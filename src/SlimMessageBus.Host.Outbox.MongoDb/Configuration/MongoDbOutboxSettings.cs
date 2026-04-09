namespace SlimMessageBus.Host.Outbox.MongoDb.Configuration;

public class MongoDbOutboxSettings : OutboxSettings
{
    public MongoDbSettings MongoDbSettings { get; set; } = new();
}

public class MongoDbSettings
{
    /// <summary>
    /// The name of the MongoDB database to store outbox messages in.
    /// </summary>
    public string DatabaseName { get; set; } = "slimmessagebus";

    /// <summary>
    /// The MongoDB collection name for outbox messages.
    /// </summary>
    public string CollectionName { get; set; } = "smb_outbox";

    /// <summary>
    /// The MongoDB collection name for the global table-level lock document (used when <see cref="OutboxSettings.MaintainSequence"/> is true).
    /// </summary>
    public string LockCollectionName { get; set; } = "smb_outbox_lock";

    /// <summary>
    /// The MongoDB collection name used to track which schema migrations have been applied.
    /// </summary>
    public string MigrationsCollectionName { get; set; } = "smb_outbox_migrations";

    /// <summary>
    /// When <c>true</c> (the default), <see cref="MongoDbOutboxMigrationService"/> will create
    /// collections and indices on bus start.  Set to <c>false</c> when you manage schema changes
    /// externally and want to prevent SMB from touching the database schema at startup.
    /// </summary>
    public bool EnableMigration { get; set; } = true;
}
