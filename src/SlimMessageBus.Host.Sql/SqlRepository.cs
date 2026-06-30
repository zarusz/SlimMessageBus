namespace SlimMessageBus.Host.Sql;

public class SqlRepository : CommonSqlRepository, ISqlRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new ObjectToInferredTypesConverter() }
    };

    private readonly SqlMessageBusSettings _settings;
    private readonly SqlTemplate _template;

    public SqlRepository(ILogger<SqlRepository> logger, SqlMessageBusSettings settings, SqlTemplate template, SqlConnection connection, ISqlTransactionService transactionService)
        : base(logger, settings, connection, transactionService)
    {
        _settings = settings;
        _template = template;
    }

    public async Task UpsertSubscription(string path, string subscriptionName, CancellationToken cancellationToken)
    {
        await EnsureConnection();
        await ExecuteNonQuery(_settings.OperationRetry, _template.UpsertSubscription, cmd =>
        {
            cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = path;
            cmd.Parameters.Add("@SubscriptionName", SqlDbType.NVarChar).Value = subscriptionName;
        }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetSubscriptions(string path, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _template.GetSubscriptions;
        cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = path;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var subscriptions = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            subscriptions.Add(reader.GetString(0));
        }

        return subscriptions;
    }

    public async Task Insert(SqlTransportMessage message, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        var insertSql = _settings.IdGeneration.Mode switch
        {
            SqlMessageIdGenerationMode.DatabaseGeneratedGuid => _template.InsertWithDatabaseGuid,
            SqlMessageIdGenerationMode.DatabaseGeneratedSequentialGuid => _template.InsertWithDatabaseSequentialGuid,
            _ => _template.InsertWithClientId
        };

        await ExecuteScalarAsync(_settings.OperationRetry, insertSql, cmd =>
        {
            if (_settings.IdGeneration.Mode == SqlMessageIdGenerationMode.ClientGuidGenerator)
            {
                cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = message.Id;
            }
            cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = message.Path;
            cmd.Parameters.Add("@PathKind", SqlDbType.TinyInt).Value = (byte)message.PathKind;
            cmd.Parameters.Add("@SubscriptionName", SqlDbType.NVarChar).Value = (object)message.SubscriptionName ?? DBNull.Value;
            cmd.Parameters.Add("@MessageType", SqlDbType.NVarChar).Value = message.MessageType;
            cmd.Parameters.Add("@MessagePayload", SqlDbType.VarBinary).Value = message.MessagePayload;
            cmd.Parameters.Add("@Headers", SqlDbType.NVarChar).Value = message.Headers != null ? JsonSerializer.Serialize(message.Headers, _jsonOptions) : DBNull.Value;
        }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SqlTransportMessage>> LockAndSelect(string path, PathKind pathKind, string subscriptionName, string instanceId, int batchSize, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection();

        using var cmd = CreateCommand();
        cmd.CommandText = _template.LockAndSelect;
        cmd.Parameters.Add("@Path", SqlDbType.NVarChar).Value = path;
        cmd.Parameters.Add("@PathKind", SqlDbType.TinyInt).Value = (byte)pathKind;
        cmd.Parameters.Add("@SubscriptionName", SqlDbType.NVarChar).Value = (object)subscriptionName ?? DBNull.Value;
        cmd.Parameters.Add("@InstanceId", SqlDbType.NVarChar).Value = instanceId;
        cmd.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;
        cmd.Parameters.Add("@LockDuration", SqlDbType.Int).Value = (int)lockDuration.TotalSeconds;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var idOrdinal = reader.GetOrdinal("Id");
        var pathOrdinal = reader.GetOrdinal("Path");
        var pathKindOrdinal = reader.GetOrdinal("PathKind");
        var subscriptionNameOrdinal = reader.GetOrdinal("SubscriptionName");
        var messageTypeOrdinal = reader.GetOrdinal("MessageType");
        var messagePayloadOrdinal = reader.GetOrdinal("MessagePayload");
        var headersOrdinal = reader.GetOrdinal("Headers");

        var messages = new List<SqlTransportMessage>(batchSize);
        while (await reader.ReadAsync(cancellationToken))
        {
            var headers = reader.IsDBNull(headersOrdinal)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(headersOrdinal), _jsonOptions);

            messages.Add(new SqlTransportMessage
            {
                Id = reader.GetGuid(idOrdinal),
                Path = reader.GetString(pathOrdinal),
                PathKind = (PathKind)reader.GetByte(pathKindOrdinal),
                SubscriptionName = reader.IsDBNull(subscriptionNameOrdinal) ? null : reader.GetString(subscriptionNameOrdinal),
                MessageType = reader.GetString(messageTypeOrdinal),
                MessagePayload = reader.GetSqlBinary(messagePayloadOrdinal).Value,
                Headers = headers
            });
        }

        return messages;
    }

    public async Task Complete(IReadOnlyCollection<SqlTransportMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await ExecuteNonQuery(_settings.OperationRetry, _template.Complete, cmd =>
        {
            cmd.Parameters.Add("@Ids", SqlDbType.NVarChar).Value = ToIdsString(messages);
        }, cancellationToken);
    }

    public async Task Fail(IReadOnlyCollection<SqlTransportMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await ExecuteNonQuery(_settings.OperationRetry, _template.Fail, cmd =>
        {
            cmd.Parameters.Add("@Ids", SqlDbType.NVarChar).Value = ToIdsString(messages);
            cmd.Parameters.Add("@MaxDeliveryAttempts", SqlDbType.Int).Value = maxDeliveryAttempts;
        }, cancellationToken);
    }

    private static string ToIdsString(IEnumerable<SqlTransportMessage> messages) => string.Join("|", messages.Select(x => x.Id));
}
