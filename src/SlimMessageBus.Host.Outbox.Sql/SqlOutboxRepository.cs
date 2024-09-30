namespace SlimMessageBus.Host.Outbox.Sql;
public class SqlOutboxRepository : CommonSqlRepository, ISqlOutboxRepository
{
    private readonly SqlOutboxTemplate _sqlTemplate;
    private readonly IOutboxMessageAdapter _outboxMessageAdapter;
    private readonly JsonSerializerOptions _jsonOptions;

    protected SqlOutboxSettings Settings { get; }

    public SqlOutboxRepository(ILogger<SqlOutboxRepository> logger, SqlOutboxSettings settings, SqlOutboxTemplate sqlOutboxTemplate, SqlConnection connection, ISqlTransactionService transactionService, IOutboxMessageAdapter outboxMessageAdapter)
        : base(logger, settings.SqlSettings, connection, transactionService)
    {
        _sqlTemplate = sqlOutboxTemplate;
        _outboxMessageAdapter = outboxMessageAdapter;
        _jsonOptions = new();
        _jsonOptions.Converters.Add(new ObjectToInferredTypesConverter());

        Settings = settings;
    }

    public OutboxMessage Create() => _outboxMessageAdapter.Create();

    public async virtual Task Save(OutboxMessage message, CancellationToken token)
    {
        await EnsureConnection();

        await ExecuteNonQuery(Settings.SqlSettings.OperationRetry, _sqlTemplate.SqlOutboxMessageInsert, cmd =>
        {
            cmd.Parameters.Add(_outboxMessageAdapter.CreateIdSqlParameter("@Id", message));
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

    public async Task AbortDelivery(IReadOnlyCollection<OutboxMessage> messages, CancellationToken token)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageAbortDelivery,
            cmd =>
            {
                cmd.Parameters.Add(_outboxMessageAdapter.CreateIdsSqlParameter("@Ids", messages));
            },
            token: token);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    public async Task UpdateToSent(IReadOnlyCollection<OutboxMessage> messages, CancellationToken token)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageUpdateSent,
            cmd =>
            {
                cmd.Parameters.Add(_outboxMessageAdapter.CreateIdsSqlParameter("@Ids", messages));
            },
            token: token);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    private string ToIdsString(IReadOnlyCollection<Guid> ids) => string.Join(_sqlTemplate.InIdsSeparator, ids);

    public async Task IncrementDeliveryAttempt(IReadOnlyCollection<OutboxMessage> messages, int maxDeliveryAttempts, CancellationToken token)
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
            _sqlTemplate.SqlOutboxMessageIncrementDeliveryAttempt,
            cmd =>
            {
                cmd.Parameters.Add(_outboxMessageAdapter.CreateIdsSqlParameter("@Ids", messages));
                cmd.Parameters.AddWithValue("@MaxDeliveryAttempts", maxDeliveryAttempts);
            },
            token: token);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
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
            var headers = reader.IsDBNull(headersOrdinal) ? null : reader.GetString(headersOrdinal);

            var message = _outboxMessageAdapter.Create(reader, idOrdinal);

            message.Timestamp = reader.GetDateTime(timestampOrdinal);
            message.BusName = reader.GetString(busNameOrdinal);
            message.MessageType = reader.GetString(typeOrdinal);
            message.MessagePayload = reader.GetSqlBinary(payloadOrdinal).Value;
            message.Headers = headers == null ? null : JsonSerializer.Deserialize<IDictionary<string, object>>(headers, _jsonOptions);
            message.Path = reader.IsDBNull(pathOrdinal) ? null : reader.GetString(pathOrdinal);
            message.InstanceId = reader.GetString(instanceIdOrdinal);
            message.LockInstanceId = reader.IsDBNull(lockInstanceIdOrdinal) ? null : reader.GetString(lockInstanceIdOrdinal);
            message.LockExpiresOn = reader.IsDBNull(lockExpiresOnOrdinal) ? null : reader.GetDateTime(lockExpiresOnOrdinal);
            message.DeliveryAttempt = reader.GetInt32(deliveryAttemptOrdinal);
            message.DeliveryComplete = reader.GetBoolean(deliveryCompleteOrdinal);
            message.DeliveryAborted = reader.GetBoolean(deliveryAbortedOrdinal);

            items.Add(message);
        }

        return items;
    }
}
