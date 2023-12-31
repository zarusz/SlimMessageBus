namespace SlimMessageBus.Host.Outbox.Sql;

public class SqlOutboxRepository : CommonSqlRepository, ISqlOutboxRepository
{
    private readonly SqlOutboxTemplate _sqlTemplate;
    private readonly JsonSerializerOptions _jsonOptions;

    protected SqlOutboxSettings Settings { get; }

    public SqlOutboxRepository(ILogger<SqlOutboxRepository> logger, SqlOutboxSettings settings, SqlOutboxTemplate sqlOutboxTemplate, SqlConnection connection)
        : base(logger, settings.SqlSettings, connection)
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
            cmd.Parameters.Add("@MessageType", SqlDbType.NVarChar).Value = Settings.MessageTypeResolver.ToName(message.MessageType);
            cmd.Parameters.Add("@MessagePayload", SqlDbType.VarBinary).Value = message.MessagePayload;
            cmd.Parameters.Add("@Headers", SqlDbType.NVarChar).Value = message.Headers != null ? JsonSerializer.Serialize(message.Headers, _jsonOptions) : DBNull.Value;
            cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = message.Path;
            cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = message.InstanceId;
            cmd.Parameters.Add("@LockInstanceId", SqlDbType.NVarChar).Value = message.LockInstanceId;
            cmd.Parameters.Add("@LockExpiresOn", SqlDbType.DateTime2).Value = message.LockExpiresOn;
            cmd.Parameters.Add("@DeliveryAttempt", SqlDbType.Int).Value = message.DeliveryAttempt;
            cmd.Parameters.Add("@DeliveryComplete", SqlDbType.Bit).Value = message.DeliveryComplete;
        }, token);
    }

    public async Task<IReadOnlyList<OutboxMessage>> FindNextToSend(string instanceId, CancellationToken token)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _sqlTemplate.SqlOutboxMessageFindNextSelect;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;

        using var reader = await cmd.ExecuteReaderAsync(token);

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
                MessageType = Settings.MessageTypeResolver.ToType(messageType) ?? throw new MessageBusException($"Outbox message with Id {id} - the MessageType {messageType} is not recognized. The type might have been renamed or moved namespaces."),
                MessagePayload = reader.GetSqlBinary(payloadOrdinal).Value,
                Headers = headers == null ? null : JsonSerializer.Deserialize<IDictionary<string, object>>(headers, _jsonOptions),
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

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry,
            @$"UPDATE {_sqlTemplate.TableNameQualified} SET [DeliveryComplete] = 1 WHERE [Id] IN ({string.Join(",", ids.Select(id => string.Concat("'", id, "'")))})",
            token: token);

        if (affected != ids.Count)
        {
            throw new MessageBusException($"The number of affected rows was {affected}, but {ids.Count} was expected");
        }
    }

    public async Task<int> TryToLock(string instanceId, DateTime expiresOn, CancellationToken token)
    {
        await EnsureConnection();

        // Extend the lease if still the owner of it, or claim the lease if another instace had possesion, but it expired (or message never was locked)
        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry, _sqlTemplate.SqlOutboxMessageTryLockUpdate, cmd =>
        {
            cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
            cmd.Parameters.Add("@ExpiresOn", SqlDbType.DateTime2).Value = expiresOn;
        }, token);

        return affected;
    }

    public async Task DeleteSent(DateTime olderThan, CancellationToken token)
    {
        await EnsureConnection();

        var affected = await ExecuteNonQuery(Settings.SqlSettings.OperationRetry, _sqlTemplate.SqlOutboxMessageDeleteSent, cmd =>
        {
            cmd.Parameters.Add("@Timestamp", SqlDbType.DateTime2).Value = olderThan;
        }, token);

        Logger.Log(affected > 0 ? LogLevel.Information : LogLevel.Debug, "Removed {MessageCount} sent messages from outbox table", affected);
    }
}
