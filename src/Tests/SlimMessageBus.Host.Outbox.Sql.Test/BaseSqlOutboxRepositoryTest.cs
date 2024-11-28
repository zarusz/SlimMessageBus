namespace SlimMessageBus.Host.Outbox.Sql.Test;

public class BaseSqlOutboxRepositoryTest : BaseSqlTest
{
    protected readonly Fixture _fixture = new();

    protected SqlConnection _connection;
    protected SqlOutboxMigrationService _migrationService;
    protected SqlOutboxSettings _settings;
    protected SqlOutboxMessageRepository _target;
    protected SqlOutboxTemplate _template;
    protected ISqlTransactionService _transactionService;
    protected CurrentTimeProviderFake _currentTimeProvider;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _settings = new SqlOutboxSettings();
        _connection = new SqlConnection(GetConnectionString());
        _transactionService = new SqlTransactionService(_connection, _settings.SqlSettings);
        _template = new SqlOutboxTemplate(_settings);
        _currentTimeProvider = new();
        _target = new SqlOutboxMessageRepository(NullLogger<SqlOutboxMessageRepository>.Instance, _settings, _template, new GuidGenerator(), _currentTimeProvider, new DefaultInstanceIdProvider(), _connection, _transactionService);
        _migrationService = new SqlOutboxMigrationService(NullLogger<SqlOutboxMigrationService>.Instance, _target, _transactionService, _settings);

        await _migrationService.Migrate(CancellationToken.None);
    }

    public override Task DisposeAsync()
    {
        _connection.Dispose();
        return base.DisposeAsync();
    }

    protected async Task<IReadOnlyList<SqlOutboxMessage>> SeedOutbox(int count, Action<int, SqlOutboxMessage> action = null, CancellationToken cancellationToken = default)
    {
        var messages = CreateOutboxMessages(count);
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            action?.Invoke(i, message);
            message.Id = (Guid)(await _target.Create(message.BusName, message.Headers, message.Path, message.MessageType, message.MessagePayload, cancellationToken)).Id;
        }
        return messages;
    }

    protected IReadOnlyList<SqlOutboxMessage> CreateOutboxMessages(int count) => Enumerable
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
            _fixture.Customize<SqlOutboxMessage>(om => om
                .With(x => x.MessagePayload, jsonPayload)
                .With(x => x.Headers, headers)
                .With(x => x.LockExpiresOn, DateTime.MinValue)
                .With(x => x.LockInstanceId, string.Empty)
                .With(x => x.DeliveryAborted, false)
                .With(x => x.DeliveryAttempt, 0)
                .With(x => x.DeliveryComplete, false));

            return _fixture.Create<SqlOutboxMessage>();
        })
        .ToList();
}
