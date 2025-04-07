namespace SlimMessageBus.Host.Outbox.PostgreSql.Repositories;

public class PostgreSqlOutboxMessageRepository : IPostgreSqlMessageOutboxRepository
{
    /// <summary>
    /// Used to serialize the headers dictionary to JSON
    /// </summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new ObjectToInferredTypesConverter() }
    };

    private static readonly DateTimeOffset _defaultExpiresOn = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ILogger _logger;
    private readonly PostgreSqlSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly IPostgreSqlOutboxTemplate _sqlTemplate;
    private readonly IPostgreSqlTransactionService _transactionService;

    public PostgreSqlOutboxMessageRepository(
        ILogger logger,
        PostgreSqlSettings settings,
        TimeProvider timeProvider,
        IPostgreSqlOutboxTemplate sqlTemplate,
        NpgsqlConnection connection,
        IPostgreSqlTransactionService transactionService)
    {
        _logger = logger;
        _settings = settings;
        _timeProvider = timeProvider;
        _sqlTemplate = sqlTemplate;
        Connection = connection;
        _transactionService = transactionService;
    }

    public NpgsqlConnection Connection { get; }

    public async Task<OutboxMessage> Create(string busName, IDictionary<string, object> headers, string path, string messageType, byte[] messagePayload, CancellationToken cancellationToken)
    {
        var om = new PostgreSqlOutboxAdminMessage
        {
            Id = Guid.NewGuid(),
            Timestamp = _timeProvider.GetUtcNow(),
            BusName = busName,
            Headers = headers,
            Path = path,
            MessageType = messageType,
            MessagePayload = messagePayload,
        };

        await EnsureConnection(cancellationToken);
        await ExecuteScalarAsync(_settings.OperationRetry, _sqlTemplate.Insert, cmd =>
        {
            cmd.Parameters.Add("id", NpgsqlDbType.Uuid).Value = om.Id;
            cmd.Parameters.Add("timestamp", NpgsqlDbType.TimestampTz).Value = om.Timestamp.UtcDateTime;
            cmd.Parameters.Add("bus_name", NpgsqlDbType.Text).Value = om.BusName;
            cmd.Parameters.Add("message_type", NpgsqlDbType.Text).Value = om.MessageType;
            cmd.Parameters.Add("message_payload", NpgsqlDbType.Bytea).Value = om.MessagePayload;
            cmd.Parameters.Add("headers", NpgsqlDbType.Jsonb).Value = om.Headers != null ? JsonSerializer.Serialize(om.Headers, _jsonOptions) : DBNull.Value;
            cmd.Parameters.Add("path", NpgsqlDbType.Text).Value = om.Path;
            cmd.Parameters.Add("lock_instance_id", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(om.LockInstanceId) ? DBNull.Value : om.LockInstanceId;
            cmd.Parameters.Add("lock_expires_on", NpgsqlDbType.TimestampTz).Value = om.LockExpiresOn ?? _defaultExpiresOn;
            cmd.Parameters.Add("delivery_attempt", NpgsqlDbType.Integer).Value = om.DeliveryAttempt;
            cmd.Parameters.Add("delivery_complete", NpgsqlDbType.Boolean).Value = om.DeliveryComplete;
            cmd.Parameters.Add("delivery_aborted", NpgsqlDbType.Boolean).Value = om.DeliveryAborted;
        }, cancellationToken);

        return om;
    }

    public async Task AbortDelivery(IReadOnlyCollection<PostgreSqlOutboxMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var ids = IdList(messages);

        await EnsureConnection(cancellationToken);
        var affected = await ExecuteNonQuery(_settings.OperationRetry,
            _sqlTemplate.AbortDelivery,
            cmd => cmd.Parameters.AddWithValue("ids", ids),
            cancellationToken: cancellationToken);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    public async Task<int> DeleteSent(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken);

        var affected = await ExecuteNonQuery(
            _settings.OperationRetry,
            _sqlTemplate.DeleteSent,
            cmd =>
            {
                cmd.Parameters.Add("batch_size", NpgsqlDbType.Integer).Value = batchSize;
                cmd.Parameters.Add("timestamp", NpgsqlDbType.TimestampTz).Value = olderThan.ToUniversalTime();
            },
            cancellationToken);

        _logger.Log(affected > 0 ? LogLevel.Information : LogLevel.Debug, "Removed {MessageCount} sent messages from outbox table", affected);

        return affected;
    }

    public async Task IncrementDeliveryAttempt(IReadOnlyCollection<PostgreSqlOutboxMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        if (maxDeliveryAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDeliveryAttempts), "Must be larger than 0.");
        }

        var ids = IdList(messages);

        await EnsureConnection(cancellationToken);
        var affected = await ExecuteNonQuery(_settings.OperationRetry,
            _sqlTemplate.IncrementDeliveryAttempt,
            cmd =>
            {
                cmd.Parameters.AddWithValue("ids", ids);
                cmd.Parameters.AddWithValue("max_delivery_attempts", maxDeliveryAttempts);
            },
            cancellationToken: cancellationToken);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    public async Task<IReadOnlyCollection<PostgreSqlOutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken);

        using var cmd = CreateCommand();
        cmd.CommandText = tableLock ? _sqlTemplate.LockTableAndSelect : _sqlTemplate.LockAndSelect;
        cmd.Parameters.Add("instance_id", NpgsqlDbType.Text).Value = instanceId;
        cmd.Parameters.Add("batch_size", NpgsqlDbType.Integer).Value = batchSize;
        cmd.Parameters.Add("lock_duration", NpgsqlDbType.Interval).Value = lockDuration;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var busNameOrdinal = reader.GetOrdinal("bus_name");
        var typeOrdinal = reader.GetOrdinal("message_type");
        var payloadOrdinal = reader.GetOrdinal("message_payload");
        var headersOrdinal = reader.GetOrdinal("headers");
        var pathOrdinal = reader.GetOrdinal("path");

        var items = new List<PostgreSqlOutboxMessage>(batchSize);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var headers = await reader.IsDBNullAsync(headersOrdinal, cancellationToken)
                ? null
                : reader.GetString(headersOrdinal);

            var message = new PostgreSqlOutboxMessage
            {
                Id = reader.GetGuid(idOrdinal),
                BusName = reader.GetString(busNameOrdinal),
                MessageType = reader.GetString(typeOrdinal),
                MessagePayload = await reader.GetFieldValueAsync<byte[]>(payloadOrdinal, cancellationToken),
                Headers = headers == null
                    ? null
                    : JsonSerializer.Deserialize<IDictionary<string, object>>(headers, _jsonOptions),
                Path = await reader.IsDBNullAsync(pathOrdinal, cancellationToken)
                    ? null
                    : reader.GetString(pathOrdinal)
            };

            items.Add(message);
        }

        return items;
    }

    public async Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken);

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.RenewLock;
        cmd.Parameters.Add("instance_id", NpgsqlDbType.Text).Value = instanceId;
        cmd.Parameters.Add("lock_duration", NpgsqlDbType.Interval).Value = lockDuration;

        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task UpdateToSent(IReadOnlyCollection<PostgreSqlOutboxMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var ids = IdList(messages);

        await EnsureConnection(cancellationToken);
        var affected = await ExecuteNonQuery(_settings.OperationRetry,
            _sqlTemplate.UpdateSent,
            cmd => cmd.Parameters.AddWithValue("ids", ids),
            cancellationToken: cancellationToken);

        if (affected != messages.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {messages.Count} was expected");
        }
    }

    public Task<int> ExecuteNonQuery(RetrySettings retrySettings, string sql, Action<NpgsqlCommand>? setParameters = null, CancellationToken cancellationToken = default) =>
        PostgreSqlHelper.RetryIfTransientError(_logger, retrySettings, async () =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = sql;
            setParameters?.Invoke(cmd);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);

    public Task<object?> ExecuteScalarAsync(RetrySettings retrySettings, string sql, Action<NpgsqlCommand>? setParameters = null, CancellationToken cancellationToken = default) =>
        PostgreSqlHelper.RetryIfTransientError(_logger, retrySettings, async () =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = sql;
            setParameters?.Invoke(cmd);
            return await cmd.ExecuteScalarAsync(cancellationToken);
        }, cancellationToken);

    public async Task EnsureConnection(CancellationToken cancellationToken)
    {
        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken);
        }
    }

    internal async Task<IReadOnlyCollection<PostgreSqlOutboxAdminMessage>> GetAllMessages(CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken);

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.AllMessages;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("id");
        var timestampOrdinal = reader.GetOrdinal("timestamp");
        var busNameOrdinal = reader.GetOrdinal("bus_name");
        var typeOrdinal = reader.GetOrdinal("message_type");
        var payloadOrdinal = reader.GetOrdinal("message_payload");
        var headersOrdinal = reader.GetOrdinal("headers");
        var pathOrdinal = reader.GetOrdinal("path");
        var lockInstanceIdOrdinal = reader.GetOrdinal("lock_instance_id");
        var lockExpiresOnOrdinal = reader.GetOrdinal("lock_expires_on");
        var deliveryAttemptOrdinal = reader.GetOrdinal("delivery_attempt");
        var deliveryCompleteOrdinal = reader.GetOrdinal("delivery_complete");
        var deliveryAbortedOrdinal = reader.GetOrdinal("delivery_aborted");

        var items = new List<PostgreSqlOutboxAdminMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var headers = await reader.IsDBNullAsync(headersOrdinal, cancellationToken)
                ? null
                : reader.GetString(headersOrdinal);

            var message = new PostgreSqlOutboxAdminMessage
            {
                Id = reader.GetGuid(idOrdinal),
                Timestamp = reader.GetDateTime(timestampOrdinal),
                BusName = reader.GetString(busNameOrdinal),
                MessageType = reader.GetString(typeOrdinal),
                MessagePayload = await reader.GetFieldValueAsync<byte[]>(payloadOrdinal, cancellationToken),
                Headers = headers == null
                    ? null
                    : JsonSerializer.Deserialize<IDictionary<string, object>>(headers, _jsonOptions),
                Path = await reader.IsDBNullAsync(pathOrdinal, cancellationToken)
                    ? null
                    : reader.GetString(pathOrdinal),
                LockInstanceId = await reader.IsDBNullAsync(lockInstanceIdOrdinal, cancellationToken)
                    ? null
                    : reader.GetString(lockInstanceIdOrdinal),
                LockExpiresOn = await reader.IsDBNullAsync(lockExpiresOnOrdinal, cancellationToken)
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

    protected virtual NpgsqlCommand CreateCommand()
    {
        var cmd = Connection.CreateCommand();
        cmd.Transaction = _transactionService.CurrentTransaction;
        if (_settings.CommandTimeout != null)
        {
            cmd.CommandTimeout = (int)_settings.CommandTimeout.Value.TotalSeconds;
        }

        return cmd;
    }

    private static IReadOnlyList<Guid> IdList(IReadOnlyCollection<PostgreSqlOutboxMessage> messages)
    {
        var list = new List<Guid>(messages.Count);
        list.AddRange(messages.Select(m => m.Id));
        return list;
    }
}
