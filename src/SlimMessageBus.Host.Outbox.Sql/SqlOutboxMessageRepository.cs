namespace SlimMessageBus.Host.Outbox.Sql;

using System;

/// <summary>
/// The MS SQL implementation of the <see cref="IOutboxMessageRepository{TOutboxMessage}"/>
/// </summary>
public class SqlOutboxMessageRepository : CommonSqlRepository, ISqlMessageOutboxRepository
{
    /// <summary>
    /// Used to serialize the headers dictionary to JSON
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new ObjectToInferredTypesConverter() }
    };

    private static readonly DateTime _defaultExpiresOn = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISqlOutboxTemplate _sqlTemplate;
    private readonly IGuidGenerator _guidGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly IInstanceIdProvider _instanceIdProvider;
    private readonly bool _idDatabaseGenerated;

    protected SqlOutboxSettings Settings { get; }

    public SqlOutboxMessageRepository(
        ILogger<SqlOutboxMessageRepository> logger,
        SqlOutboxSettings settings,
        ISqlOutboxTemplate sqlOutboxTemplate,
        IGuidGenerator guidGenerator,
        TimeProvider timeProvider,
        IInstanceIdProvider instanceIdProvider,
        SqlConnection connection,
        ISqlTransactionService transactionService)
        : base(logger, settings.SqlSettings, connection, transactionService)
    {
        _sqlTemplate = sqlOutboxTemplate;
        _guidGenerator = guidGenerator;
        _timeProvider = timeProvider;
        _instanceIdProvider = instanceIdProvider;
        _idDatabaseGenerated = settings.IdGeneration.Mode == SqlOutboxMessageIdGenerationMode.DatabaseGeneratedSequentialGuid;
        Settings = settings;
    }

    public virtual async Task<OutboxMessage> Create(string busName, IDictionary<string, object> headers, string path, string messageType, byte[] messagePayload, CancellationToken cancellationToken)
    {
        var om = new SqlOutboxAdminMessage
        {
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            InstanceId = _instanceIdProvider.GetInstanceId(),

            BusName = busName,
            Headers = headers,
            Path = path,
            MessageType = messageType,
            MessagePayload = messagePayload,
        };

        if (!_idDatabaseGenerated)
        {
            om.Id = _guidGenerator.NewGuid();
        }

        var template = _idDatabaseGenerated
            ? _sqlTemplate.InsertWithDatabaseId
            : _sqlTemplate.InsertWithClientId;

        await EnsureConnection();

        om.Id = (Guid)await ExecuteScalarAsync(Settings.SqlSettings.OperationRetry, template, cmd =>
        {
            if (!_idDatabaseGenerated)
            {
                cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = om.Id;
            }
            cmd.Parameters.Add("@Timestamp", SqlDbType.DateTime2).Value = om.Timestamp;
            cmd.Parameters.Add("@BusName", SqlDbType.NVarChar).Value = om.BusName;
            cmd.Parameters.Add("@MessageType", SqlDbType.NVarChar).Value = om.MessageType;
            cmd.Parameters.Add("@MessagePayload", SqlDbType.VarBinary).Value = om.MessagePayload;
            cmd.Parameters.Add("@Headers", SqlDbType.NVarChar).Value = om.Headers != null ? JsonSerializer.Serialize(om.Headers, _jsonOptions) : DBNull.Value;
            cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = om.Path;
            cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = om.InstanceId;
            cmd.Parameters.Add("@LockInstanceId", SqlDbType.NVarChar).Value = om.LockInstanceId;
            cmd.Parameters.Add("@LockExpiresOn", SqlDbType.DateTime2).Value = om.LockExpiresOn ?? _defaultExpiresOn;
            cmd.Parameters.Add("@DeliveryAttempt", SqlDbType.Int).Value = om.DeliveryAttempt;
            cmd.Parameters.Add("@DeliveryComplete", SqlDbType.Bit).Value = om.DeliveryComplete;
            cmd.Parameters.Add("@DeliveryAborted", SqlDbType.Bit).Value = om.DeliveryAborted;
        }, cancellationToken);

        return om;
    }

    public async Task<IReadOnlyCollection<SqlOutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = tableLock ? _sqlTemplate.LockTableAndSelect : _sqlTemplate.LockAndSelect;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
        cmd.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        cmd.Parameters.Add("@LockDuration", SqlDbType.Int).Value = lockDuration.TotalSeconds;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("Id");
        var busNameOrdinal = reader.GetOrdinal("BusName");
        var typeOrdinal = reader.GetOrdinal("MessageType");
        var payloadOrdinal = reader.GetOrdinal("MessagePayload");
        var headersOrdinal = reader.GetOrdinal("Headers");
        var pathOrdinal = reader.GetOrdinal("Path");

        var items = new List<SqlOutboxMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var headers = reader.IsDBNull(headersOrdinal)
                ? null
                : reader.GetString(headersOrdinal);

            var message = new SqlOutboxMessage
            {
                Id = reader.GetGuid(idOrdinal),
                BusName = reader.GetString(busNameOrdinal),
                MessageType = reader.GetString(typeOrdinal),
                MessagePayload = reader.GetSqlBinary(payloadOrdinal).Value,
                Headers = headers == null
                    ? null
                    : JsonSerializer.Deserialize<IDictionary<string, object>>(headers, _jsonOptions),
                Path = reader.IsDBNull(pathOrdinal)
                    ? null
                    : reader.GetString(pathOrdinal)
            };

            items.Add(message);
        }

        return items;
    }

    public async Task AbortDelivery(IReadOnlyCollection<SqlOutboxMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.AbortDelivery,
            cmd => cmd.Parameters.AddWithValue("@Ids", ToIdsString(messages)),
            token: cancellationToken);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    public async Task UpdateToSent(IReadOnlyCollection<SqlOutboxMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.UpdateSent,
            cmd => cmd.Parameters.AddWithValue("@Ids", ToIdsString(messages)),
            token: cancellationToken);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    private string ToIdsString(IReadOnlyCollection<SqlOutboxMessage> messages) => string.Join(_sqlTemplate.InIdsSeparator, messages.Select(x => x.Id));

    public async Task IncrementDeliveryAttempt(IReadOnlyCollection<SqlOutboxMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        if (maxDeliveryAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDeliveryAttempts), "Must be larger than 0.");
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.IncrementDeliveryAttempt,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@Ids", ToIdsString(messages));
                cmd.Parameters.AddWithValue("@MaxDeliveryAttempts", maxDeliveryAttempts);
            },
            token: cancellationToken);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    public async Task<int> DeleteSent(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        var affected = await ExecuteNonQuery(
            Settings.SqlSettings.OperationRetry,
            _sqlTemplate.DeleteSent,
            cmd =>
            {
                cmd.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
                cmd.Parameters.Add("@Timestamp", SqlDbType.DateTimeOffset).Value = olderThan;
            },
            cancellationToken);

        Logger.Log(affected > 0 ? LogLevel.Information : LogLevel.Debug, "Removed {MessageCount} sent messages from outbox table", affected);

        return affected;
    }

    public async Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.RenewLock;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
        cmd.Parameters.Add("@LockDuration", SqlDbType.Int).Value = lockDuration.TotalSeconds;

        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    internal async Task<IReadOnlyCollection<SqlOutboxAdminMessage>> GetAllMessages(CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.AllMessages;

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

        var items = new List<SqlOutboxAdminMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var headers = reader.IsDBNull(headersOrdinal)
                ? null
                : reader.GetString(headersOrdinal);

            var message = new SqlOutboxAdminMessage
            {
                Id = reader.GetGuid(idOrdinal),
                Timestamp = reader.GetDateTime(timestampOrdinal),
                BusName = reader.GetString(busNameOrdinal),
                MessageType = reader.GetString(typeOrdinal),
                MessagePayload = reader.GetSqlBinary(payloadOrdinal).Value,
                Headers = headers == null
                    ? null
                    : JsonSerializer.Deserialize<IDictionary<string, object>>(headers, _jsonOptions),
                Path = reader.IsDBNull(pathOrdinal)
                    ? null
                    : reader.GetString(pathOrdinal),
                InstanceId = reader.GetString(instanceIdOrdinal),
                LockInstanceId = reader.IsDBNull(lockInstanceIdOrdinal)
                    ? null
                    : reader.GetString(lockInstanceIdOrdinal),
                LockExpiresOn = reader.IsDBNull(lockExpiresOnOrdinal)
                    ? null
                    : reader.GetDateTime(lockExpiresOnOrdinal),
                DeliveryAttempt = reader.GetInt32(deliveryAttemptOrdinal),
                DeliveryComplete = reader.GetBoolean(deliveryCompleteOrdinal),
                DeliveryAborted = reader.GetBoolean(deliveryAbortedOrdinal)
            };

            items.Add(message);
        }

        return items;
    }
}
