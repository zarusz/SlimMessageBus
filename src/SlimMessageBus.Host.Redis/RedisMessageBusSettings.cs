namespace SlimMessageBus.Host.Redis
{
    using System;
    using StackExchange.Redis;

    public class RedisMessageBusSettings
    {
        /// <summary>
        /// The <see cref="ConnectionMultiplexer.Configuration"/> configuration setting.
        /// </summary>
        public string Configuration { get; set; }
        /// <summary>
        /// Allows to override the default <see cref="ConnectionMultiplexer"/> factory.
        /// </summary>
        public Func<IConnectionMultiplexer> ConnectionFactory { get; set; }
        /// <summary>
        /// If true the consumers are started when message bus is created, otherwise you need to call <see cref="RedisMessageBus.Start"/> manually.
        /// </summary>
        public bool AutoStartConsumers { get; set; }
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

        public RedisMessageBusSettings(string configuration)
        {
            Configuration = configuration;
            ConnectionFactory = () => ConnectionMultiplexer.Connect(Configuration);
            AutoStartConsumers = true;
        }
    }
}
