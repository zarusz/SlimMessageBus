namespace SlimMessageBus.Host.Outbox.Sql.Test;

[Collection(nameof(SqlServerCollection))]
public class BaseSqlOutboxRepositoryTest : IAsyncLifetime
{
    protected readonly Fixture _fixture = new();

    private readonly SqlServerFixture _sqlServerFixture;

    protected SqlConnection _connection;
    protected SqlOutboxMigrationService _migrationService;
    protected SqlOutboxSettings _settings;
    protected SqlOutboxMessageRepository _target;
    protected ISqlOutboxTemplate _template;
    protected ISqlTransactionService _transactionService;
    protected FakeTimeProvider _currentTimeProvider;

    protected BaseSqlOutboxRepositoryTest(SqlServerFixture sqlServerFixture)
    {
        _sqlServerFixture = sqlServerFixture;
    }

    public async Task InitializeAsync()
    {
        var schemaName = Debugger.IsAttached ? "smb" : $"smb_{Guid.NewGuid().ToString("N").ToLowerInvariant()}";
        await _sqlServerFixture.CreateSchema(schemaName);

        _settings = new SqlOutboxSettings();
        _settings.SqlSettings.DatabaseSchemaName = schemaName;
        _connection = new SqlConnection(_sqlServerFixture.GetConnectionString());
        _transactionService = new SqlTransactionService(_connection, _settings.SqlSettings);
        _template = new SqlOutboxTemplate(_settings);
        _currentTimeProvider = new();
        _target = new SqlOutboxMessageRepository(NullLogger<SqlOutboxMessageRepository>.Instance, _settings, _template, new GuidGenerator(), _currentTimeProvider, new DefaultInstanceIdProvider(), _connection, _transactionService);
        _migrationService = new SqlOutboxMigrationService(NullLogger<SqlOutboxMigrationService>.Instance, _target, _transactionService, _settings);

        await _migrationService.Migrate(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<IReadOnlyCollection<SqlOutboxMessage>> SeedOutbox(int count, Action<int, SqlOutboxAdminMessage> action = null, CancellationToken cancellationToken = default)
    {
        var messages = CreateOutboxMessages(count);
        var results = new List<SqlOutboxMessage>(count);
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            action?.Invoke(i, message);
            results.Add((SqlOutboxMessage)await _target.Create(message.BusName, message.Headers, message.Path, message.MessageType, message.MessagePayload, cancellationToken));
        }

        return results;
    }

    protected IReadOnlyList<SqlOutboxAdminMessage> CreateOutboxMessages(int count)
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
                _fixture.Customize<SqlOutboxAdminMessage>(om => om
                    .With(x => x.MessagePayload, jsonPayload)
                    .With(x => x.Headers, headers)
                    .With(x => x.LockExpiresOn, DateTime.MinValue)
                    .With(x => x.LockInstanceId, string.Empty)
                    .With(x => x.DeliveryAborted, false)
                    .With(x => x.DeliveryAttempt, 0)
                    .With(x => x.DeliveryComplete, false));

                return _fixture.Create<SqlOutboxAdminMessage>();
            })
            .ToList();
    }
}