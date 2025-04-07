namespace SlimMessageBus.Host.Outbox.PostgreSql.Test;

[Collection(nameof(PostgreSqlCollection))]
public class BasePostgreSqlOutboxRepositoryTest : IAsyncLifetime
{
    protected readonly Fixture _fixture = new();

    private readonly PostgreSqlFixture _postgreSqlFixture;

    protected NpgsqlConnection _connection;
    protected PostgreSqlOutboxMigrationService _migrationService;
    protected PostgreSqlOutboxSettings _settings;
    protected PostgreSqlOutboxMessageRepository _target;
    protected IPostgreSqlTransactionService _transactionService;
    protected FakeTimeProvider _currentTimeProvider;
    protected PostgreSqlOutboxTemplate _template;

    protected BasePostgreSqlOutboxRepositoryTest(PostgreSqlFixture postgreSqlFixture)
    {
        _postgreSqlFixture = postgreSqlFixture;
    }

    public async Task InitializeAsync()
    {
        var schemaName = Debugger.IsAttached ? "slim" : $"slim_{Guid.NewGuid().ToString("N").ToLowerInvariant()}";
        await _postgreSqlFixture.CreateSchema(schemaName);

        _settings = new PostgreSqlOutboxSettings();
        _settings.PostgreSqlSettings.DatabaseSchemaName = schemaName;
        _connection = new NpgsqlConnection(_postgreSqlFixture.GetConnectionString());
        _transactionService = new PostgreSqlTransactionService(_connection, _settings.PostgreSqlSettings);
        _currentTimeProvider = new();
        _template = new PostgreSqlOutboxTemplate(_settings.PostgreSqlSettings);
        _target = new PostgreSqlOutboxMessageRepository(NullLogger<PostgreSqlOutboxMessageRepository>.Instance, _settings.PostgreSqlSettings, _currentTimeProvider, _template, _connection, _transactionService);
        _migrationService = new PostgreSqlOutboxMigrationService(NullLogger<PostgreSqlOutboxMigrationService>.Instance, _settings, _target, _template);

        await _migrationService.Migrate(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<IReadOnlyCollection<PostgreSqlOutboxMessage>> SeedOutbox(int count, Action<int, PostgreSqlOutboxAdminMessage> action = null, CancellationToken cancellationToken = default)
    {
        var messages = CreateOutboxMessages(count);
        var results = new List<PostgreSqlOutboxMessage>(count);
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            action?.Invoke(i, message);
            results.Add((PostgreSqlOutboxMessage)await _target.Create(message.BusName, message.Headers, message.Path, message.MessageType, message.MessagePayload, cancellationToken));
        }

        return results;
    }

    protected IReadOnlyList<PostgreSqlOutboxAdminMessage> CreateOutboxMessages(int count)
    {
        return Enumerable
            .Range(0, count)
            .Select(_ =>
            {
                // Create a sample object for MessagePayload
                var samplePayload = new { Key = _fixture.Create<string>(), Number = _fixture.Create<int>() };
                var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(samplePayload);

                // Generate Headers dictionary with simple types
                var headers = new Dictionary<string, object>
                {
                    { "Header1", _fixture.Create<string>() },
                    { "Header2", _fixture.Create<int>() },
                    { "Header3", _fixture.Create<bool>() }
                };

                // Configure fixture to use the generated values
                _fixture.Customize<PostgreSqlOutboxAdminMessage>(om => om
                    .With(x => x.MessagePayload, jsonPayload)
                    .With(x => x.Headers, headers)
                    .With(x => x.LockExpiresOn, DateTimeOffset.MinValue)
                    .With(x => x.LockInstanceId, string.Empty)
                    .With(x => x.DeliveryAborted, false)
                    .With(x => x.DeliveryAttempt, 0)
                    .With(x => x.DeliveryComplete, false));

                return _fixture.Create<PostgreSqlOutboxAdminMessage>();
            })
            .ToList();
    }
}
