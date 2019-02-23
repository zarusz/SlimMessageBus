using System;
using StackExchange.Redis;

namespace SlimMessageBus.Host.Redis
{
    public class RedisMessageBusSettings
    {
        /// <summary>
        /// The <see cref="ConnectionMultiplexer.Configuration"/> configuration setting.
        /// </summary>
        public string Configuration { get; set; }
        /// <summary>
        /// Allows to override the default <see cref="ConnectionMultiplexer"/> factory.
        /// </summary>
        public Func<ConnectionMultiplexer> ConnectionFactory { get; set; }
        /// <summary>
        /// If true the consumers are started when message bus is created, otherwise you need to call <see cref="RedisMessageBus.Start"/> manually.
        /// </summary>
        public bool AutoStartConsumers { get; set; }

        public RedisMessageBusSettings(string configuration)
        {
            Configuration = configuration;
            ConnectionFactory = () => ConnectionMultiplexer.Connect(Configuration);
            AutoStartConsumers = true;
        }
    }
}
