using StackExchange.Redis;
using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host.Redis
{
    public class RedisMessageBusSettings
    {
        public IEnumerable<string> Servers { get; set; }

        public int SyncTimeout { get; set; }

        public int DatabaseIndex { get; set; }

        /// <summary>
        /// Factory method that creates a <see cref="ConfigurationOptions"/>. 
        /// </summary>
        public Func<ConfigurationOptions> ConfigurationOptionsFactory { get; set; }

        /// <summary>
        /// Factory method that creates a <see cref="ConnectionMultiplexer"/>. 
        /// </summary>
        public Func<ConfigurationOptions, ConnectionMultiplexer> ConnectionMultiplexerFactory { get; set; }

        public RedisMessageBusSettings(string server, int syncTimeout, int dbIndex = -1)
            : this(new List<string> { server }, syncTimeout, dbIndex)
        { }

        public RedisMessageBusSettings(IEnumerable<string> servers, int syncTimeout, int dbIndex)
        {
            Servers = servers;
            SyncTimeout = syncTimeout;
            DatabaseIndex = dbIndex;

            ConfigurationOptionsFactory = () =>
            {
                var configurationOptions = new ConfigurationOptions
                {
                    SyncTimeout = SyncTimeout
                };

                foreach (var server in Servers)
                {
                    configurationOptions.EndPoints.Add(server);
                }

                return configurationOptions;
            };

            ConnectionMultiplexerFactory = (config) => ConnectionMultiplexer.Connect(config);
        }
    }
}
