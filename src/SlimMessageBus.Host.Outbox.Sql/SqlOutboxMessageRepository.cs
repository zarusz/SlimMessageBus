namespace SlimMessageBus.Host.Outbox.Sql;

/// <summary>
/// The MS SQL implmentation of the <see cref="IOutboxMessageRepository"/>
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

    private readonly SqlOutboxTemplate _sqlTemplate;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTimeProvider _currentTimeProvider;
    private readonly IInstanceIdProvider _instanceIdProvider;
    private readonly bool _idDatabaseGenerated;

    protected SqlOutboxSettings Settings { get; }

    public SqlOutboxMessageRepository(
        ILogger<SqlOutboxMessageRepository> logger,
        SqlOutboxSettings settings,
        SqlOutboxTemplate sqlOutboxTemplate,
        IGuidGenerator guidGenerator,
        ICurrentTimeProvider currentTimeProvider,
        IInstanceIdProvider instanceIdProvider,
        SqlConnection connection,
        ISqlTransactionService transactionService)
        : base(logger, settings.SqlSettings, connection, transactionService)
    {
        _sqlTemplate = sqlOutboxTemplate;
        _guidGenerator = guidGenerator;
        _currentTimeProvider = currentTimeProvider;
        _instanceIdProvider = instanceIdProvider;
        _idDatabaseGenerated = settings.IdGeneration.Mode == OutboxMessageIdGenerationMode.DatabaseGeneratedSequentialGuid;
        Settings = settings;
    }

    public virtual async Task<Guid> Create(string busName, IDictionary<string, object> headers, string path, string messageType, byte[] messagePayload, CancellationToken cancellationToken)
    {
        var om = new OutboxMessage
        {
            Timestamp = _currentTimeProvider.CurrentTime.UtcDateTime,
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
            ? _sqlTemplate.SqlOutboxMessageInsertWithDatabaseId
            : _sqlTemplate.SqlOutboxMessageInsertWithClientId;

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
            cmd.Parameters.Add("@LockExpiresOn", SqlDbType.DateTime2).Value = om.LockExpiresOn ?? new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            cmd.Parameters.Add("@DeliveryAttempt", SqlDbType.Int).Value = om.DeliveryAttempt;
            cmd.Parameters.Add("@DeliveryComplete", SqlDbType.Bit).Value = om.DeliveryComplete;
            cmd.Parameters.Add("@DeliveryAborted", SqlDbType.Bit).Value = om.DeliveryAborted;
        }, cancellationToken);

        return om.Id;
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> LockAndSelect(string instanceId, int batchSize, bool tableLock, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = tableLock ? _sqlTemplate.SqlOutboxMessageLockTableAndSelect : _sqlTemplate.SqlOutboxMessageLockAndSelect;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
        cmd.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        cmd.Parameters.Add("@LockDuration", SqlDbType.Int).Value = lockDuration.TotalSeconds;

        return await ReadMessages(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task AbortDelivery(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageAbortDelivery,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@Ids", ToIdsString(ids));
            },
            token: cancellationToken);

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    public async Task UpdateToSent(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageUpdateSent,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@Ids", ToIdsString(ids));
            },
            token: cancellationToken);

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    private string ToIdsString(IReadOnlyCollection<Guid> ids) => string.Join(_sqlTemplate.InIdsSeparator, ids);

    public async Task IncrementDeliveryAttempt(IReadOnlyCollection<Guid> ids, int maxDeliveryAttempts, CancellationToken cancellationToken)
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

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageIncrementDeliveryAttempt,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@Ids", ToIdsString(ids));
                cmd.Parameters.AddWithValue("@MaxDeliveryAttempts", maxDeliveryAttempts);
            },
            token: cancellationToken);

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    public async Task DeleteSent(DateTime olderThan, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        var affected = await ExecuteNonQuery(
            Settings.SqlSettings.OperationRetry,
            _sqlTemplate.SqlOutboxMessageDeleteSent,
            cmd => cmd.Parameters.Add("@Timestamp", SqlDbType.DateTimeOffset).Value = olderThan,
            cancellationToken);

        Logger.Log(affected > 0 ? LogLevel.Information : LogLevel.Debug, "Removed {MessageCount} sent messages from outbox table", affected);
    }

    public async Task<bool> RenewLock(string instanceId, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.SqlOutboxMessageRenewLock;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
        cmd.Parameters.Add("@LockDuration", SqlDbType.Int).Value = lockDuration.TotalSeconds;

        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    internal async Task<IReadOnlyCollection<OutboxMessage>> GetAllMessages(CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.SqlOutboxAllMessages;

        return await ReadMessages(cmd, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyCollection<OutboxMessage>> ReadMessages(SqlCommand cmd, CancellationToken cancellationToken)
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
            var headers = reader.IsDBNull(headersOrdinal)
                ? null
                : reader.GetString(headersOrdinal);
            var message = new OutboxMessage
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
