using Common.Logging;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Redis.Consumer;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SlimMessageBus.Host.Redis
{
    /// <summary>
    /// <see cref="IMessageBus"/> implementation for Redis.
    /// </summary>
    public class RedisMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<RedisMessageBus>();
        private readonly List<RedisConsumer> _consumers = new List<RedisConsumer>();

        private IDatabase _redis;
        private ISubscriber _pub;

        public RedisMessageBusSettings RedisSettings { get; }

        public RedisMessageBus(MessageBusSettings settings, RedisMessageBusSettings redisSettings) 
            : base(settings)
        {
            AssertSettings(settings);

            RedisSettings = redisSettings;

            _redis = GetDatabase();

            //create a publisher
            _pub = _redis.Multiplexer.GetSubscriber();

            CreateConsumers(settings);
        }

        private IDatabase GetDatabase()
        {
            Log.Trace("Creating connection to the Redis server");
            var config = RedisSettings.ConfigurationOptionsFactory();

            Log.DebugFormat(CultureInfo.InvariantCulture, "Configuration options: {0}", config);
            var connection = RedisSettings.ConnectionMultiplexerFactory(config);

            if (RedisSettings.DatabaseIndex < 0)
            {
                return connection.GetDatabase();
            }

            return connection.GetDatabase(RedisSettings.DatabaseIndex);
        }

        private void CreateConsumers(MessageBusSettings settings)
        {
            Log.Info("Creating consumers");

            foreach (var consumerSettings in settings.Consumers)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating consumer for Topic: {0}, MessageType: {1}", consumerSettings.Topic, consumerSettings.MessageType);

                var consumer = new RedisConsumer(this, consumerSettings);

                _pub.Subscribe(consumerSettings.Topic, (channel, message) => {
                    consumer.OnSubmit(new RedisMessage { Value = message });
                });

                _consumers.Add(consumer);
            }

        }

        private static void AssertSettings(MessageBusSettings settings)
        {
            if (settings.RequestResponse != null)
            {
                Assert.IsFalse(settings.Consumers.Any(x => x.Topic == settings.RequestResponse.Topic),
                    () => new InvalidConfigurationMessageBusException($"Request-response: cannot use topic that is already being used by a consumer"));
            }
        }

        public override async Task PublishToTransport(Type messageType, object message, string topic, byte[] payload)
        {
            AssertActive();

            Log.TraceFormat(CultureInfo.InvariantCulture, "Producing message of type {0} on topic {1} with size {2}", messageType.Name, topic, payload.Length);

            var count = await _pub.PublishAsync(topic, payload).ConfigureAwait(false);

            // log some debug information
            Log.DebugFormat(CultureInfo.InvariantCulture, "Message of type {0} delivered to {1} subscribers", messageType.Name, count);
        }
    }
}
