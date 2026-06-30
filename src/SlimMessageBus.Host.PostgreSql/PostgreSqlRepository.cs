namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlRepository : IPostgreSqlRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new ObjectToInferredTypesConverter() }
    };

    private readonly ILogger<PostgreSqlRepository> _logger;
    private readonly PostgreSqlMessageBusSettings _settings;
    private readonly PostgreSqlTemplate _template;

    public PostgreSqlRepository(ILogger<PostgreSqlRepository> logger, PostgreSqlMessageBusSettings settings, PostgreSqlTemplate template, NpgsqlConnection connection)
    {
        _logger = logger;
        _settings = settings;
        _template = template;
        Connection = connection;
    }

    public NpgsqlConnection Connection { get; }

    public async Task EnsureConnection(CancellationToken cancellationToken)
    {
        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UpsertSubscription(string path, string subscriptionName, CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQuery(_settings.OperationRetry, _template.UpsertSubscription, cmd =>
        {
            cmd.Parameters.Add("path", NpgsqlDbType.Text).Value = path;
            cmd.Parameters.Add("subscription_name", NpgsqlDbType.Text).Value = subscriptionName;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<string>> GetSubscriptions(string path, CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken).ConfigureAwait(false);

        using var cmd = CreateCommand();
        cmd.CommandText = _template.GetSubscriptions;
        cmd.Parameters.Add("path", NpgsqlDbType.Text).Value = path;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var subscriptions = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            subscriptions.Add(reader.GetString(0));
        }

        return subscriptions;
    }

    public async Task Insert(PostgreSqlTransportMessage message, CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken).ConfigureAwait(false);

        var insertSql = _settings.IdGeneration.Mode switch
        {
            PostgreSqlMessageIdGenerationMode.DatabaseRandomUuid => _template.InsertWithDatabaseRandomUuid,
            _ => _template.InsertWithClientId
        };

        await ExecuteScalarAsync(_settings.OperationRetry, insertSql, cmd =>
        {
            if (_settings.IdGeneration.Mode == PostgreSqlMessageIdGenerationMode.ClientGuidGenerator)
            {
                cmd.Parameters.Add("id", NpgsqlDbType.Uuid).Value = message.Id;
            }
            cmd.Parameters.Add("path", NpgsqlDbType.Text).Value = message.Path;
            cmd.Parameters.Add("path_kind", NpgsqlDbType.Smallint).Value = (short)message.PathKind;
            cmd.Parameters.Add("subscription_name", NpgsqlDbType.Text).Value = (object?)message.SubscriptionName ?? DBNull.Value;
            cmd.Parameters.Add("message_type", NpgsqlDbType.Text).Value = message.MessageType;
            cmd.Parameters.Add("message_payload", NpgsqlDbType.Bytea).Value = message.MessagePayload;
            cmd.Parameters.Add("headers", NpgsqlDbType.Jsonb).Value = message.Headers != null ? JsonSerializer.Serialize(message.Headers, _jsonOptions) : DBNull.Value;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<PostgreSqlTransportMessage>> LockAndSelect(string path, PathKind pathKind, string? subscriptionName, string instanceId, int batchSize, TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        await EnsureConnection(cancellationToken).ConfigureAwait(false);

        using var cmd = CreateCommand();
        cmd.CommandText = _template.LockAndSelect;
        cmd.Parameters.Add("path", NpgsqlDbType.Text).Value = path;
        cmd.Parameters.Add("path_kind", NpgsqlDbType.Smallint).Value = (short)pathKind;
        cmd.Parameters.Add("subscription_name", NpgsqlDbType.Text).Value = (object?)subscriptionName ?? DBNull.Value;
        cmd.Parameters.Add("instance_id", NpgsqlDbType.Text).Value = instanceId;
        cmd.Parameters.Add("batch_size", NpgsqlDbType.Integer).Value = batchSize;
        cmd.Parameters.Add("lock_duration", NpgsqlDbType.Interval).Value = lockDuration;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var idOrdinal = reader.GetOrdinal("id");
        var pathOrdinal = reader.GetOrdinal("path");
        var pathKindOrdinal = reader.GetOrdinal("path_kind");
        var subscriptionNameOrdinal = reader.GetOrdinal("subscription_name");
        var messageTypeOrdinal = reader.GetOrdinal("message_type");
        var messagePayloadOrdinal = reader.GetOrdinal("message_payload");
        var headersOrdinal = reader.GetOrdinal("headers");

        var messages = new List<PostgreSqlTransportMessage>(batchSize);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var headers = await reader.IsDBNullAsync(headersOrdinal, cancellationToken).ConfigureAwait(false)
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(headersOrdinal), _jsonOptions);

            messages.Add(new PostgreSqlTransportMessage
            {
                Id = reader.GetGuid(idOrdinal),
                Path = reader.GetString(pathOrdinal),
                PathKind = (PathKind)reader.GetInt16(pathKindOrdinal),
                SubscriptionName = await reader.IsDBNullAsync(subscriptionNameOrdinal, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(subscriptionNameOrdinal),
                MessageType = reader.GetString(messageTypeOrdinal),
                MessagePayload = await reader.GetFieldValueAsync<byte[]>(messagePayloadOrdinal, cancellationToken).ConfigureAwait(false),
                Headers = headers
            });
        }

        return messages;
    }

    public async Task Complete(IReadOnlyCollection<PostgreSqlTransportMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await ExecuteNonQuery(_settings.OperationRetry, _template.Complete, cmd =>
        {
            cmd.Parameters.AddWithValue("ids", messages.Select(x => x.Id).ToArray());
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task Fail(IReadOnlyCollection<PostgreSqlTransportMessage> messages, int maxDeliveryAttempts, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await ExecuteNonQuery(_settings.OperationRetry, _template.Fail, cmd =>
        {
            cmd.Parameters.AddWithValue("ids", messages.Select(x => x.Id).ToArray());
            cmd.Parameters.Add("max_delivery_attempts", NpgsqlDbType.Integer).Value = maxDeliveryAttempts;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task Notify(string path, CancellationToken cancellationToken)
    {
        if (!_settings.NotifyOnPublish)
        {
            return;
        }

        await ExecuteNonQuery(_settings.OperationRetry, _template.Notify, cmd =>
        {
            cmd.Parameters.Add("channel", NpgsqlDbType.Text).Value = "smb_messages";
            cmd.Parameters.Add("payload", NpgsqlDbType.Text).Value = path;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ExecuteNonQuery(RetrySettings retrySettings, string sql, Action<NpgsqlCommand>? setParameters = null, CancellationToken cancellationToken = default)
    {
        var result = await PostgreSqlHelper.RetryIfTransientError(_logger, retrySettings, async () =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = sql;
            setParameters?.Invoke(cmd);
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    public Task<object?> ExecuteScalarAsync(RetrySettings retrySettings, string sql, Action<NpgsqlCommand>? setParameters = null, CancellationToken cancellationToken = default) =>
        PostgreSqlHelper.RetryIfTransientError(_logger, retrySettings, async () =>
        {
            using var cmd = CreateCommand();
            cmd.CommandText = sql;
            setParameters?.Invoke(cmd);
            return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private NpgsqlCommand CreateCommand()
    {
        var cmd = Connection.CreateCommand();
        if (_settings.CommandTimeout != null)
        {
            cmd.CommandTimeout = (int)_settings.CommandTimeout.Value.TotalSeconds;
        }

        return cmd;
    }
}
