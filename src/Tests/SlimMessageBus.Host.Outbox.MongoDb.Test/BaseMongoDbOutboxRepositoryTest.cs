namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

[Collection(nameof(MongoDbCollection))]
public class BaseMongoDbOutboxRepositoryTest : IAsyncLifetime
{
    protected readonly Fixture _fixture = new();

    private readonly MongoDbFixture _mongoDbFixture;
    private IMongoClient _client = null!;
    private IMongoDatabase _database = null!;

    protected MongoDbOutboxMessageRepository _target = null!;
    protected MongoDbOutboxMigrationService _migrationService = null!;
    protected MongoDbOutboxSettings _settings = null!;
    protected FakeTimeProvider _currentTimeProvider = null!;

    protected BaseMongoDbOutboxRepositoryTest(MongoDbFixture mongoDbFixture)
    {
        _mongoDbFixture = mongoDbFixture;
    }

    public async Task InitializeAsync()
    {
        // Each test uses its own database to achieve isolation
        var dbName = Debugger.IsAttached ? "smb_outbox_test" : $"smb_outbox_{Guid.NewGuid():N}";

        _settings = new MongoDbOutboxSettings();
        _settings.MongoDbSettings.DatabaseName = dbName;

        _client = _mongoDbFixture.CreateClient();
        _database = _client.GetDatabase(dbName);

        _currentTimeProvider = new FakeTimeProvider();

        var mongoDbSettings = _settings.MongoDbSettings;
        var outboxCollection = _database.GetCollection<MongoDbOutboxDocument>(mongoDbSettings.CollectionName);
        var lockCollection = _database.GetCollection<MongoDbOutboxLockDocument>(mongoDbSettings.LockCollectionName);

        var transactionService = new NullMongoDbTransactionService();

        _target = new MongoDbOutboxMessageRepository(
            NullLogger<MongoDbOutboxMessageRepository>.Instance,
            _currentTimeProvider,
            outboxCollection,
            lockCollection,
            transactionService);

        _migrationService = new MongoDbOutboxMigrationService(
            NullLogger<MongoDbOutboxMigrationService>.Instance,
            mongoDbSettings,
            _database);

        await _migrationService.Migrate(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        // Drop the test database to clean up
        await _client.DropDatabaseAsync(_settings.MongoDbSettings.DatabaseName);
        _client.Dispose();
    }

    internal async Task<IReadOnlyCollection<MongoDbOutboxMessage>> SeedOutbox(
        int count,
        Action<int, MongoDbOutboxAdminMessage>? action = null,
        CancellationToken cancellationToken = default)
    {
        var messages = CreateOutboxMessages(count);
        var results = new List<MongoDbOutboxMessage>(count);

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            action?.Invoke(i, message);
            results.Add((MongoDbOutboxMessage)await _target.Create(
                message.BusName,
                message.Headers,
                message.Path,
                message.MessageType,
                message.MessagePayload,
                cancellationToken));
        }

        return results;
    }

    internal IReadOnlyList<MongoDbOutboxAdminMessage> CreateOutboxMessages(int count)
    {
        return Enumerable
            .Range(0, count)
            .Select(_ =>
            {
                var samplePayload = new { Key = _fixture.Create<string>(), Number = _fixture.Create<int>() };
                var jsonPayload = JsonSerializer.SerializeToUtf8Bytes(samplePayload);

                var headers = new Dictionary<string, object>
                {
                    { "Header1", _fixture.Create<string>() },
                    { "Header2", _fixture.Create<int>() },
                    { "Header3", _fixture.Create<bool>() }
                };

                _fixture.Customize<MongoDbOutboxAdminMessage>(om => om
                    .With(x => x.MessagePayload, jsonPayload)
                    .With(x => x.Headers, (IDictionary<string, object>)headers)
                    .With(x => x.LockExpiresOn, DateTime.MinValue)
                    .With(x => x.LockInstanceId, (string?)null)
                    .With(x => x.DeliveryAborted, false)
                    .With(x => x.DeliveryAttempt, 0)
                    .With(x => x.DeliveryComplete, false));

                return _fixture.Create<MongoDbOutboxAdminMessage>();
            })
            .ToList();
    }

    /// <summary>
    /// A no-op transaction service for tests that don't need transactions.
    /// </summary>
    private sealed class NullMongoDbTransactionService : IMongoDbTransactionService
    {
        public IClientSessionHandle? CurrentSession => null;
        public Task BeginTransaction() => Task.CompletedTask;
        public Task CommitTransaction() => Task.CompletedTask;
        public Task RollbackTransaction() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
