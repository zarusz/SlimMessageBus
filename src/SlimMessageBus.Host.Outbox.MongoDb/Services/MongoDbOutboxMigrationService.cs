namespace SlimMessageBus.Host.Outbox.MongoDb.Services;

public partial class MongoDbOutboxMigrationService : IOutboxMigrationService
{
    private readonly ILogger<MongoDbOutboxMigrationService> _logger;
    private readonly MongoDbSettings _settings;
    private readonly IMongoDatabase _database;

    public MongoDbOutboxMigrationService(
        ILogger<MongoDbOutboxMigrationService> logger,
        MongoDbSettings settings,
        IMongoDatabase database)
    {
        _logger = logger;
        _settings = settings;
        _database = database;
    }

    public async Task Migrate(CancellationToken token)
    {
        if (!_settings.EnableMigration)
        {
            _logger.LogDebug("MongoDB outbox migration is disabled (EnableMigration=false), skipping.");
            return;
        }

        _logger.LogInformation("Running MongoDB outbox migrations...");

        var migrations = _database.GetCollection<MongoDbMigrationDocument>(_settings.MigrationsCollectionName);

        await TryApplyMigration(migrations, "20240101000000_SMB_Init",
            () => InitialMigration(token),
            token);

        _logger.LogInformation("MongoDB outbox migrations complete.");
    }

    /// <summary>
    /// Applies a named migration with at-least-once semantics.
    /// <para>
    /// MongoDB does not permit DDL operations (e.g. <c>createIndex</c>) inside multi-document
    /// transactions, so this method is intentionally NOT transactional. Safety is achieved
    /// through idempotency instead:
    /// <list type="bullet">
    ///   <item>The migration record is written AFTER the action succeeds, so a crash mid-action
    ///   causes a retry on the next startup rather than a silent skip.</item>
    ///   <item>Concurrent instances racing on the same migration are safe because all supported
    ///   actions (index creation) are idempotent.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Important:</b> Only add migration actions that are safe to run more than once
    /// (i.e. use <c>IF NOT EXISTS</c> semantics). Destructive one-shot operations are not
    /// supported and must be applied externally with <see cref="MongoDbSettings.EnableMigration"/>
    /// set to <c>false</c>.
    /// </para>
    /// </summary>
    private async Task<bool> TryApplyMigration(
        IMongoCollection<MongoDbMigrationDocument> migrations,
        string migrationId,
        Func<Task> action,
        CancellationToken token)
    {
        var alreadyApplied = await migrations
            .Find(Builders<MongoDbMigrationDocument>.Filter.Eq(d => d.Id, migrationId))
            .AnyAsync(token);

        if (alreadyApplied)
        {
            LogMigrationAlreadyApplied(_logger, migrationId);
            return false;
        }

        LogApplyingMigration(_logger, migrationId);
        await action();

        // Record the migration AFTER the action succeeds so that a crash mid-action
        // causes a retry on the next startup rather than being silently skipped.
        try
        {
            await migrations.InsertOneAsync(
                new MongoDbMigrationDocument
                {
                    Id = migrationId,
                    AppliedAt = DateTime.UtcNow,
                    ProductVersion = GetType().Assembly.GetName().Version?.ToString() ?? "unknown"
                },
                options: null,
                token);

            LogMigrationApplied(_logger, migrationId);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another instance recorded the same migration concurrently - that is fine.
            LogMigrationConcurrentlyRecorded(_logger, ex, migrationId);
        }

        return true;
    }

    // ── Versioned migration steps ────────────────────────────────────────────

    /// <summary>Initial schema: creates outbox and lock collection indices.</summary>
    private async Task InitialMigration(CancellationToken token)
    {
        await EnsureOutboxCollectionIndexes(token);
        await EnsureLockCollectionIndexes(token);
    }

    // ── Index helpers ────────────────────────────────────────────────────────

    private async Task EnsureOutboxCollectionIndexes(CancellationToken token)
    {
        var collection = _database.GetCollection<MongoDbOutboxDocument>(_settings.CollectionName);

        // Primary polling query: delivery_complete=false, delivery_aborted=false, ordered by timestamp
        await EnsureIndex(collection, new CreateIndexModel<MongoDbOutboxDocument>(
            Builders<MongoDbOutboxDocument>.IndexKeys
                .Ascending(d => d.DeliveryComplete)
                .Ascending(d => d.DeliveryAborted)
                .Ascending(d => d.Timestamp),
            new CreateIndexOptions
            {
                Name = "ix_smb_outbox_delivery_complete_aborted_timestamp",
                Background = true
            }),
            token);

        // Lock-ownership queries (RenewLock, LockAndSelect)
        await EnsureIndex(collection, new CreateIndexModel<MongoDbOutboxDocument>(
            Builders<MongoDbOutboxDocument>.IndexKeys
                .Ascending(d => d.LockInstanceId)
                .Ascending(d => d.LockExpiresOn),
            new CreateIndexOptions
            {
                Name = "ix_smb_outbox_lock_instance_id_expires_on",
                Background = true
            }),
            token);

        // Cleanup ordering (DeleteSent)
        await EnsureIndex(collection, new CreateIndexModel<MongoDbOutboxDocument>(
            Builders<MongoDbOutboxDocument>.IndexKeys
                .Ascending(d => d.Timestamp),
            new CreateIndexOptions
            {
                Name = "ix_smb_outbox_timestamp",
                Background = true
            }),
            token);
    }

    private async Task EnsureLockCollectionIndexes(CancellationToken token)
    {
        var collection = _database.GetCollection<MongoDbOutboxLockDocument>(_settings.LockCollectionName);

        await EnsureIndex(collection, new CreateIndexModel<MongoDbOutboxLockDocument>(
            Builders<MongoDbOutboxLockDocument>.IndexKeys.Ascending(d => d.LockExpiresOn),
            new CreateIndexOptions
            {
                Name = "ix_smb_outbox_lock_expires_on",
                Background = true
            }),
            token);
    }

    private async Task EnsureIndex<T>(
        IMongoCollection<T> collection,
        CreateIndexModel<T> indexModel,
        CancellationToken token)
    {
        try
        {
            await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: token);
        }
        catch (MongoCommandException ex) when (
            ex.CodeName == "IndexOptionsConflict" ||
            ex.CodeName == "IndexKeySpecsConflict")
        {
            LogIndexAlreadyExists(_logger, ex, indexModel.Options.Name, collection.CollectionNamespace.CollectionName);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Migration {MigrationId} already applied, skipping.")]
    private static partial void LogMigrationAlreadyApplied(ILogger logger, string migrationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Applying migration {MigrationId}...")]
    private static partial void LogApplyingMigration(ILogger logger, string migrationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migration {MigrationId} applied.")]
    private static partial void LogMigrationApplied(ILogger logger, string migrationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Migration {MigrationId} was recorded concurrently by another instance.")]
    private static partial void LogMigrationConcurrentlyRecorded(ILogger logger, Exception ex, string migrationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Index {IndexName} already exists on {Collection}.")]
    private static partial void LogIndexAlreadyExists(ILogger logger, Exception ex, string indexName, string collection);
}
