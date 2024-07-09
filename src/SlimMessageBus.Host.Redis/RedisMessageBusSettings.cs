namespace SlimMessageBus.Host.Redis;

public class RedisMessageBusSettings
{
    /// <summary>
    /// The <see cref="ConnectionMultiplexer.Configuration"/> connection setting.
    /// </summary>
    public string ConnectionString { get; set; }
    /// <summary>
    /// Allows to override the default <see cref="ConnectionMultiplexer"/> factory.
    /// </summary>
    public Func<IConnectionMultiplexer> ConnectionFactory { get; set; }
    /// <summary>
    /// Maximum allowed idle time before polling will be delayed to save on CPU cycles.
    /// Default is 1 second.
    /// </summary>
    public TimeSpan QueuePollMaxIdle { get; set; } = TimeSpan.FromSeconds(3);
    /// <summary>
    /// Specifies the optional delay between when polling of keys that are list in the event that none of the keys have new messages.
    /// If you want to optimize latency between periods on queue retrieval set to null. However, having some small delay is advised to optimize CPU usage.
    /// Default is 1 second.
    /// </summary>        
    public TimeSpan? QueuePollDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The <see cref="IMessageSerializer"/> serializer capable of serializing <see cref="MessageWithHeaders"/> that wraps the actual message type. The wrapper is needed to transmit headers for redis transport which has no headers support.
    /// By default uses <see cref="MessageWithHeadersSerializer"/>.
    /// </summary>
    public IMessageSerializer EnvelopeSerializer { get; set; }

    /// <summary>
    /// Hook that is fired when the Redis connection to database is established on startup. Can be used to perform some Redis database cleanup or initialization.
    /// </summary>
    public Action<IDatabase> OnDatabaseConnected { get; set; }

    public RedisMessageBusSettings()
    {
        ConnectionFactory = () => ConnectionMultiplexer.Connect(ConnectionString);
        EnvelopeSerializer = new MessageWithHeadersSerializer();
    }

    public RedisMessageBusSettings(string connectionString) : this()
    {
        ConnectionString = connectionString;
    }
}
