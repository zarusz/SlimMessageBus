namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

public class MongoDbOutboxMigrationServiceTests
{
    /// <summary>
    /// Unit tests that do not require a running MongoDB instance.
    /// </summary>
    public class Unit
    {
        [Fact]
        public async Task When_Migrate_Given_MigrationDisabled_Then_DoesNotTouchDatabase()
        {
            // arrange
            var databaseMock = new Mock<IMongoDatabase>();
            var settings = new MongoDbSettings { EnableMigration = false };
            var sut = new MongoDbOutboxMigrationService(
                NullLogger<MongoDbOutboxMigrationService>.Instance,
                settings,
                databaseMock.Object);

            // act
            await sut.Migrate(CancellationToken.None);

            // assert — the database should never be touched
            databaseMock.VerifyNoOtherCalls();
        }
    }

    /// <summary>
    /// Integration tests that require a running MongoDB instance.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Transport", "Outbox.MongoDb")]
    [Collection(nameof(MongoDbCollection))]
    public class Integration(MongoDbFixture mongoDbFixture) : IAsyncLifetime
    {
        private IMongoClient _client = null!;
        private IMongoDatabase _database = null!;
        private MongoDbOutboxMigrationService _sut = null!;
        private MongoDbSettings _dbSettings = null!;

        public async Task InitializeAsync()
        {
            var dbName = $"smb_migration_test_{Guid.NewGuid():N}";
            _dbSettings = new MongoDbSettings { DatabaseName = dbName };
            _client = mongoDbFixture.CreateClient();
            _database = _client.GetDatabase(dbName);
            _sut = new MongoDbOutboxMigrationService(
                NullLogger<MongoDbOutboxMigrationService>.Instance,
                _dbSettings,
                _database);

            await _sut.Migrate(CancellationToken.None);
        }

        public async Task DisposeAsync()
        {
            await _client.DropDatabaseAsync(_dbSettings.DatabaseName);
            _client.Dispose();
        }

        [Fact]
        public async Task When_Migrate_Given_AlreadyApplied_Then_IsIdempotentAndDoesNotThrow()
        {
            // act — second call should detect already-applied migration and skip it
            var act = () => _sut.Migrate(CancellationToken.None);

            // assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task When_Migrate_Given_AlreadyApplied_Then_IndexesStillExist()
        {
            // act — run again
            await _sut.Migrate(CancellationToken.None);

            // assert — outbox collection indexes are still present
            var outboxCollection = _database.GetCollection<BsonDocument>(_dbSettings.CollectionName);
            var indexes = await outboxCollection.Indexes.List().ToListAsync();
            indexes.Count.Should().BeGreaterThan(1); // at least the default _id index + our custom ones
        }
    }
}
