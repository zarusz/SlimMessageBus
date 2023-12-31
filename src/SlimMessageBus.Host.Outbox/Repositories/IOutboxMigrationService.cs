namespace SlimMessageBus.Host.Outbox;

public interface IOutboxMigrationService
{
    /// <summary>
    /// Initializes the data schema for the outbox. Invoked once on bus start.
    /// </summary>
    /// <returns></returns>
    Task Migrate(CancellationToken token);
}