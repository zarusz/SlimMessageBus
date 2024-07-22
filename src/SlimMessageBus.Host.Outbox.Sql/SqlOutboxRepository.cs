namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxRepository : CommonSqlRepository, ISqlOutboxRepository
{
    private readonly SqlOutboxTemplate _sqlTemplate;
    private readonly JsonSerializerOptions _jsonOptions;

    protected SqlOutboxSettings Settings { get; }

    public SqlOutboxRepository(ILogger<SqlOutboxRepository> logger, SqlOutboxSettings settings, SqlOutboxTemplate sqlOutboxTemplate, SqlConnection connection, ISqlTransactionService transactionService)
        : base(logger, settings.SqlSettings, connection, transactionService)
    {
        _sqlTemplate = sqlOutboxTemplate;
        _jsonOptions = new();
        _jsonOptions.Converters.Add(new ObjectToInferredTypesConverter());

        Settings = settings;
    }

    public async virtual Task Save(OutboxMessage message, CancellationToken token)
    {
        await EnsureConnection();

        // ToDo: Create command template

        await ExecuteNonQuery(Settings.SqlSettings.OperationRetry, _sqlTemplate.SqlOutboxMessageInsert, cmd =>
        {
            cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = message.Id;
            cmd.Parameters.Add("@Timestamp", SqlDbType.DateTime2).Value = message.Timestamp;
            cmd.Parameters.Add("@BusName", SqlDbType.NVarChar).Value = message.BusName;
            cmd.Parameters.Add("@MessageType", SqlDbType.NVarChar).Value = message.MessageType;
            cmd.Parameters.Add("@MessagePayload", SqlDbType.VarBinary).Value = message.MessagePayload;
            cmd.Parameters.Add("@Headers", SqlDbType.NVarChar).Value = message.Headers != null ? JsonSerializer.Serialize(message.Headers, _jsonOptions) : DBNull.Value;
            cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = message.Path;
            cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = message.InstanceId;
            cmd.Parameters.Add("@LockInstanceId", SqlDbType.NVarChar).Value = message.LockInstanceId;
            cmd.Parameters.Add("@LockExpiresOn", SqlDbType.DateTime2).Value = message.LockExpiresOn ?? new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            cmd.Parameters.Add("@DeliveryAttempt", SqlDbType.Int).Value = message.DeliveryAttempt;
            cmd.Parameters.Add("@DeliveryComplete", SqlDbType.Bit).Value = message.DeliveryComplete;
            cmd.Parameters.Add("@DeliveryAborted", SqlDbType.Bit).Value = message.DeliveryAborted;
        }, token);
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken token)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = tableLock ? _sqlTemplate.SqlOutboxMessageLockTableAndSelect : _sqlTemplate.SqlOutboxMessageLockAndSelect;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
        cmd.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        cmd.Parameters.Add("@LockDuration", SqlDbType.Int).Value = lockDuration.TotalSeconds;

        return await ReadMessages(cmd, token).ConfigureAwait(false);
    }

    public async Task AbortDelivery(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        foreach (var guid in ids)
        {
            table.Rows.Add(guid);
        }

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageAbortDelivery,
            cmd =>
            {
                var param = cmd.Parameters.Add("@Ids", SqlDbType.Structured);
                param.TypeName = _sqlTemplate.OutboxIdTypeQualified;
                param.Value = table;
            },
            token: token);

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    public async Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        foreach (var guid in ids)
        {
            table.Rows.Add(guid);
        }

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageUpdateSent,
            cmd =>
            {
                var param = cmd.Parameters.Add("@Ids", SqlDbType.Structured);
                param.TypeName = _sqlTemplate.OutboxIdTypeQualified;
                param.Value = table;
            },
            token: token);

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    public async Task IncrementDeliveryAttempt(IReadOnlyCollection<Guid> ids, int maxDeliveryAttempts, CancellationToken token)
    {
        if (ids.Count == 0)
        {
            return;
        }

        if (maxDeliveryAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDeliveryAttempts), "Must be larger than 0.");
        }

        await EnsureConnection();

        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        foreach (var guid in ids)
        {
            table.Rows.Add(guid);
        }

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageIncrementDeliveryAttempt,
            cmd =>
            {
                var param = cmd.Parameters.Add("@Ids", SqlDbType.Structured);
                param.TypeName = _sqlTemplate.OutboxIdTypeQualified;
                param.Value = table;

                cmd.Parameters.AddWithValue("@MaxDeliveryAttempts", maxDeliveryAttempts);
            },
            token: token);

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    public async Task DeleteSent(DateTime olderThan, CancellationToken token)
    {
        await EnsureConnection();

        var affected = await ExecuteNonQuery(
            Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageDeleteSent,
            cmd => cmd.Parameters.Add("@Timestamp", SqlDbType.DateTime2).Value = olderThan,
            token);

        Logger.Log(affected > 0 ? LogLevel.Information : LogLevel.Debug, "Removed {MessageCount} sent messages from outbox table", affected);
    }

    public async Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken token)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.SqlOutboxMessageRenewLock;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
        cmd.Parameters.Add("@LockDuration", SqlDbType.Int).Value = lockDuration.TotalSeconds;

        return await cmd.ExecuteNonQueryAsync(token) > 0;
    }

    internal async Task<IReadOnlyCollection<OutboxMessage>> GetAllMessages(CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.SqlOutboxAllMessages;

        return await ReadMessages(cmd, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyCollection<OutboxMessage>> ReadMessages(SqlCommand cmd, CancellationToken cancellationToken)
    {
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

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
        var deliveryAbortedOrdinal = reader.GetOrdinal("DeliveryAborted");

        var items = new List<OutboxMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetGuid(idOrdinal);
            var headers = reader.IsDBNull(headersOrdinal) ? null : reader.GetString(headersOrdinal);
            var message = new OutboxMessage
            {
                Id = id,
                Timestamp = reader.GetDateTime(timestampOrdinal),
                BusName = reader.GetString(busNameOrdinal),
                MessageType = reader.GetString(typeOrdinal),
                MessagePayload = reader.GetSqlBinary(payloadOrdinal).Value,
                Headers = headers == null ? null : JsonSerializer.Deserialize<IDictionary<string, object>>(headers, _jsonOptions),
                Path = reader.IsDBNull(pathOrdinal) ? null : reader.GetString(pathOrdinal),
                InstanceId = reader.GetString(instanceIdOrdinal),
                LockInstanceId = reader.IsDBNull(lockInstanceIdOrdinal) ? null : reader.GetString(lockInstanceIdOrdinal),
                LockExpiresOn = reader.IsDBNull(lockExpiresOnOrdinal) ? null : reader.GetDateTime(lockExpiresOnOrdinal),
                DeliveryAttempt = reader.GetInt32(deliveryAttemptOrdinal),
                DeliveryComplete = reader.GetBoolean(deliveryCompleteOrdinal),
                DeliveryAborted = reader.GetBoolean(deliveryAbortedOrdinal)
            };

            items.Add(message);
        }

        return items;
    }
}
