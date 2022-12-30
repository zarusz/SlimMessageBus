namespace SlimMessageBus.Host.Outbox.Sql;

using System.Data;
using System.Text.Json;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

public class SqlOutboxRepository : ISqlOutboxRepository, IAsyncDisposable
{
    private readonly ILogger<SqlOutboxRepository> _logger;
    private readonly string _tableNameQualified;
    private SqlTransaction _transaction;

    protected SqlOutboxSettings Settings { get; }
    protected SqlConnection Connection { get; }

    public SqlOutboxRepository(ILogger<SqlOutboxRepository> logger, SqlOutboxSettings settings, SqlConnection connection)
    {
        _logger = logger;
        Settings = settings;
        Connection = connection;
        _tableNameQualified = $"[{Settings.DatabaseSchemaName}].[{Settings.DatabaseTableName}]";
    }

    private async Task EnsureConnection()
    {
        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync();
        }
    }

    protected virtual SqlCommand CreateCommand()
    {
        var cmd = Connection.CreateCommand();
        cmd.Transaction = CurrentTransaction;

        if (Settings.CommandTimeout != null)
        {
            cmd.CommandTimeout = (int)Settings.CommandTimeout.Value.TotalSeconds;
        }

        return cmd;
    }

    public virtual SqlTransaction CurrentTransaction => _transaction;

    public virtual async ValueTask BeginTransaction()
    {
        ValidateNoTransactionStarted();
        _transaction = (SqlTransaction)await Connection.BeginTransactionAsync(Settings.TransactionIsolationLevel);
    }

    public virtual async ValueTask CommitTransaction()
    {
        ValidateTransactionStarted();

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public virtual async ValueTask RollbackTransaction()
    {
        ValidateTransactionStarted();

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    protected void ValidateNoTransactionStarted()
    {
        if (CurrentTransaction != null)
        {
            throw new MessageBusException("Transaction is already in progress");
        }
    }

    protected void ValidateTransactionStarted()
    {
        if (CurrentTransaction == null)
        {
            throw new MessageBusException("Transaction has not been started");
        }
    }

    public virtual async Task Initialize(CancellationToken token)
    {
        await EnsureConnection();
        try
        {
            _logger.LogInformation("Outbox database schema provisioning started...");

            // Retry few times to create the schema - perhaps there are concurrently running other service process-es that attempt to do the same (distributed micro-service).
            await SqlHelper.RetryIfError(_logger, token, Settings.SchemaCreationRetry, _ => true, async () =>
            {
                await BeginTransaction();
                try
                {
                    _logger.LogDebug("Ensuring table {TableName} is created", _tableNameQualified);
                    await ExecuteNonQuery(token, Settings.SchemaCreationRetry,
                        @$"IF OBJECT_ID('{_tableNameQualified}') IS NULL 
                        BEGIN 
                            CREATE TABLE {_tableNameQualified} (
                                Id uniqueidentifier NOT NULL,
                                Timestamp datetime2(7) NOT NULL,
                                BusName nvarchar(64) NOT NULL,
                                MessageType nvarchar(256) NOT NULL,
                                MessagePayload varbinary(max) NOT NULL,
                                Headers nvarchar(max),
                                Path nvarchar(128),
                                InstanceId nvarchar(128) NOT NULL,
                                LockInstanceId nvarchar(128) NOT NULL,
                                LockExpiresOn datetime2(7) NOT NULL,
                                DeliveryAttempt int NOT NULL,
                                DeliveryComplete bit NOT NULL,
                                CONSTRAINT [PK_{Settings.DatabaseTableName}] PRIMARY KEY CLUSTERED ([Id] ASC)
                            )
                        END");

                    await CreateIndex(token, "IX_Outbox_InstanceId", new string[] {
                        "DeliveryComplete",
                        "InstanceId"
                    });

                    await CreateIndex(token, "IX_Outbox_LockExpiresOn", new string[] {
                        "DeliveryComplete",
                        "LockExpiresOn"
                    });

                    await CreateIndex(token, "IX_Outbox_Timestamp_LockInstanceId", new string[] {
                        "DeliveryComplete",
                        "Timestamp",
                        "LockInstanceId",
                    });

                    await CommitTransaction();
                    return true;
                }
                catch (Exception)
                {
                    await RollbackTransaction();
                    throw;
                }
            });

            _logger.LogInformation("Outbox database schema provisioning finished");
        }
        catch (SqlException e)
        {
            _logger.LogError(e, "Outbox database schema provisioning enocuntered a non-recoverable SQL error: {ErrorMessage}", e.Message);
            throw;
        }
    }

    private async Task CreateIndex(CancellationToken token, string indexName, IEnumerable<string> columns)
    {
        _logger.LogDebug("Ensuring index {IndexName} on table {TableName} is created", indexName, _tableNameQualified);
        await ExecuteNonQuery(token, Settings.SchemaCreationRetry,
            @$"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{_tableNameQualified}'))
            BEGIN 
                CREATE NONCLUSTERED INDEX [{indexName}] ON {_tableNameQualified}
                (
                    {string.Join(',', columns.Select(c => $"{c} ASC"))}
                )
            END");
    }

    private Task<int> ExecuteNonQuery(CancellationToken token, SqlRetrySettings retrySettings, string sql, Action<SqlCommand> setParameters = null) =>
        SqlHelper.RetryIfTransientError(_logger, token, retrySettings, async () =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = sql;
            setParameters?.Invoke(cmd);
            return await cmd.ExecuteNonQueryAsync();
        });

    public virtual async Task Save(OutboxMessage message, CancellationToken token)
    {
        await EnsureConnection();

        // ToDo: Create command template

        await ExecuteNonQuery(token, Settings.OperationRetry,
            @$"INSERT INTO {_tableNameQualified}
                ([Id], [Timestamp], [BusName], [MessageType], [MessagePayload], [Headers], [Path], [InstanceId], [LockInstanceId], [LockExpiresOn], [DeliveryAttempt], [DeliveryComplete])
            VALUES
                (@Id, @Timestamp, @BusName, @MessageType, @MessagePayload, @Headers, @Path, @InstanceId, @LockInstanceId, @LockExpiresOn, @DeliveryAttempt, @DeliveryComplete)",
            cmd =>
            {
                cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = message.Id;
                cmd.Parameters.Add("@Timestamp", SqlDbType.DateTime2).Value = message.Timestamp;
                cmd.Parameters.Add("@BusName", SqlDbType.NVarChar).Value = message.BusName;
                cmd.Parameters.Add("@MessageType", SqlDbType.NVarChar).Value = message.MessageType.AssemblyQualifiedName;
                cmd.Parameters.Add("@MessagePayload", SqlDbType.VarBinary).Value = message.MessagePayload;
                cmd.Parameters.Add("@Headers", SqlDbType.NVarChar).Value = message.Headers != null ? JsonSerializer.Serialize(message.Headers) : DBNull.Value;
                cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = message.Path;
                cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = message.InstanceId;
                cmd.Parameters.Add("@LockInstanceId", SqlDbType.NVarChar).Value = message.LockInstanceId;
                cmd.Parameters.Add("@LockExpiresOn", SqlDbType.DateTime2).Value = message.LockExpiresOn;
                cmd.Parameters.Add("@DeliveryAttempt", SqlDbType.Int).Value = message.DeliveryAttempt;
                cmd.Parameters.Add("@DeliveryComplete", SqlDbType.Bit).Value = message.DeliveryComplete;
            });
    }

    public async Task<IReadOnlyList<OutboxMessage>> FindNextToSend(int top, string instanceId, CancellationToken token)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = @$"SELECT TOP {top} * FROM {_tableNameQualified} WHERE DeliveryComplete = 0 AND LockInstanceId = @InstanceId ORDER BY Timestamp ASC";
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;

        using var reader = await cmd.ExecuteReaderAsync();

        var idOrdinal = reader.GetOrdinal("Id");
        var timestampOrdinal = reader.GetOrdinal("Timestamp");
        var busNameOrdinal = reader.GetOrdinal("BusName");
        var typeOrdinal = reader.GetOrdinal("MessageType");
        var payloadOrdinal = reader.GetOrdinal("MessagePayload");
        var headersOrdinal = reader.GetOrdinal("Headers");
        var pathOrdinal = reader.GetOrdinal("Path");
        var instanceIdOrdinal = reader.GetOrdinal("InstanceId");
        var lockInstanceIdOrdinal = reader.GetOrdinal("LockInstanceId");
        var lockExpiresOnOrdinal = reader.GetOrdinal("LockExpiresOn");
        var deliveryAttemptOrdinal = reader.GetOrdinal("DeliveryAttempt");
        var deliveryCompleteOrdinal = reader.GetOrdinal("DeliveryComplete");

        var list = new List<OutboxMessage>();

        while (await reader.ReadAsync(token))
        {
            var id = reader.GetGuid(idOrdinal);
            var messageType = reader.GetString(typeOrdinal);
            var headers = reader.IsDBNull(headersOrdinal) ? null : reader.GetString(headersOrdinal);
            var message = new OutboxMessage
            {
                Id = id,
                Timestamp = reader.GetDateTime(timestampOrdinal),
                BusName = reader.GetString(busNameOrdinal),
                MessageType = Type.GetType(messageType) ?? throw new MessageBusException($"Outbox message with Id {id} - the MessageType {messageType} is not recognized. The type might have been renamed or moved namespaces."),
                MessagePayload = reader.GetSqlBinary(payloadOrdinal).Value,
                Headers = headers == null ? null : JsonSerializer.Deserialize<IDictionary<string, object>>(headers),
                Path = reader.IsDBNull(pathOrdinal) ? null : reader.GetString(pathOrdinal),
                InstanceId = reader.GetString(instanceIdOrdinal),
                LockInstanceId = reader.IsDBNull(lockInstanceIdOrdinal) ? null : reader.GetString(lockInstanceIdOrdinal),
                LockExpiresOn = reader.IsDBNull(lockExpiresOnOrdinal) ? null : reader.GetDateTime(lockExpiresOnOrdinal),
                DeliveryAttempt = reader.GetInt32(deliveryAttemptOrdinal),
                DeliveryComplete = reader.GetBoolean(deliveryCompleteOrdinal),
            };
            list.Add(message);
        }

        return list;
    }

    public async Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(token, Settings.OperationRetry,
            @$"UPDATE {_tableNameQualified} SET [DeliveryComplete] = 1 WHERE [Id] IN ({string.Join(",", ids.Select(id => string.Concat("'", id, "'")))})");

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    public async Task<int> TryToLock(string instanceId, DateTime expiresOn, CancellationToken token)
    {
        await EnsureConnection();

        // Extend the lease if still the owner of it, or claim the lease if another instace had possesion, but it expired (or message never was locked)
        var affected = await ExecuteNonQuery(token, Settings.OperationRetry,
            @$"UPDATE {_tableNameQualified} SET LockInstanceId = @InstanceId, LockExpiresOn = @ExpiresOn WHERE DeliveryComplete = 0 AND LockExpiresOn < GetUtcDate() OR LockInstanceId = @InstanceId",
            cmd =>
            {
                cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
                cmd.Parameters.Add("@ExpiresOn", SqlDbType.DateTime2).Value = expiresOn;
            });

        return affected;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();

        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_transaction != null)
        {
            await RollbackTransaction();
        }
    }

    public async Task DeleteSent(DateTime timestampBefore, CancellationToken token)
    {
        await EnsureConnection();

        var affected = await ExecuteNonQuery(token, Settings.OperationRetry,
            @$"DELETE FROM {_tableNameQualified} WHERE [DeliveryComplete] = 1 AND [Timestamp] < @Timestamp",
            cmd =>
            {
                cmd.Parameters.Add("@Timestamp", SqlDbType.DateTime2).Value = timestampBefore;
            });

        _logger.Log(affected > 0 ? LogLevel.Information : LogLevel.Debug, "Removed {MessageCount} sent messages from outbox table", affected);
    }
}
