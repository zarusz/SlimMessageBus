namespace SlimMessageBus.Host.Sql.Common;

public class SqlRetrySettings
{
    public int RetryCount { get; set; }
    public TimeSpan RetryInterval { get; set; }
    public float RetryIntervalFactor { get; set; }
}
