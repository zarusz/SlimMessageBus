namespace SlimMessageBus.Host.Outbox.Sql.Test;

public class BaseSqlOutboxRepositoryTest : BaseSqlTest
{
    protected readonly Fixture _fixture = new();

    protected SqlConnection _connection;
    protected SqlOutboxMigrationService _migrationService;
    protected SqlOutboxSettings _settings;
    protected SqlOutboxRepository _target;
    protected SqlOutboxTemplate _template;
    protected ISqlTransactionService _transactionService;
    protected IOutboxMessageAdapter _outboxMessageAdapter;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _settings = new SqlOutboxSettings();
        _connection = new SqlConnection(GetConnectionString());
        _transactionService = new SqlTransactionService(_connection, _settings.SqlSettings);
        _template = new SqlOutboxTemplate(_settings);
        _outboxMessageAdapter = new GuidOutboxMessageAdapter(_template);
        _target = new SqlOutboxRepository(NullLogger<SqlOutboxRepository>.Instance, _settings, _template, _connection, _transactionService, _outboxMessageAdapter);
        _migrationService = new SqlOutboxMigrationService(NullLogger<SqlOutboxMigrationService>.Instance, _target, _transactionService, _settings);

        await _migrationService.Migrate(CancellationToken.None);
    }

    public override Task DisposeAsync()
    {
        _connection.Dispose();
        return base.DisposeAsync();
    }

    protected async Task<IReadOnlyList<OutboxMessage>> SeedOutbox(int count, Action<int, OutboxMessage> action = null, CancellationToken cancellationToken = default)
    {
        var messages = CreateOutboxMessages(count);
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            action?.Invoke(i, message);
            await _target.Save(message, cancellationToken);
        }

        return messages;
    }

    protected IReadOnlyList<OutboxMessage> CreateOutboxMessages(int count)
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
                _fixture.Customize<OutboxMessage>(om => om
                    .With(x => x.MessagePayload, jsonPayload)
                    .With(x => x.Headers, headers)
                    .With(x => x.LockExpiresOn, DateTime.MinValue)
                    .With(x => x.LockInstanceId, string.Empty)
                    .With(x => x.DeliveryAborted, false)
                    .With(x => x.DeliveryAttempt, 0)
                    .With(x => x.DeliveryComplete, false));

                return _fixture.Create<OutboxMessage>();
            })
            .ToList();
    }
}
