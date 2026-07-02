namespace SlimMessageBus.Host.Sql;

public class SqlMessageBusSettings : SqlSettings, IRelationalMessageBusSettings
{
    public string ConnectionString { get; set; }
    public TimeSpan PollDelay { get; set; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int PollBatchSize { get; set; } = 10;
    public int MaxDeliveryAttempts { get; set; } = 10;
    public SqlMessageIdGenerationSettings IdGeneration { get; set; } = new();
}
