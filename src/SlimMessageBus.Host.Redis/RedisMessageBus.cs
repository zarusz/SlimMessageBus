using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Common.Logging;
using SlimMessageBus.Host.AzureServiceBus.Consumer;
using SlimMessageBus.Host.Config;
using StackExchange.Redis;

namespace SlimMessageBus.Host.Redis
{
    public class RedisMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<RedisMessageBus>();

        public RedisMessageBusSettings RedisSettings { get; }

        public bool IsRunning { get; private set; }

        protected ConnectionMultiplexer Connection { get; private set; }
        protected IDatabase Database { get; private set; }

        private readonly List<RedisChannelConsumer> _consumers = new List<RedisChannelConsumer>();

        public RedisMessageBus(MessageBusSettings settings, RedisMessageBusSettings redisSettings)
            : base(settings)
        {
            RedisSettings = redisSettings;
            IsRunning = false;

            Connection = RedisSettings.ConnectionFactory();
            Database = Connection.GetDatabase();

            if (RedisSettings.AutoStartConsumers)
            {
                Start().GetAwaiter().GetResult();
            }
        }

        #region Overrides of MessageBusBase

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (IsRunning)
                {
                    Stop().GetAwaiter().GetResult();
                }
                Connection.DisposeSilently(nameof(ConnectionMultiplexer), Log);
            }
        }

        #endregion

        public virtual Task Start()
        {
            if (!IsRunning)
            {
                CreateConsumers();

                IsRunning = true;
            }
            return Task.CompletedTask;
        }

        protected void CreateConsumers()
        {
            var subscriber = Connection.GetSubscriber();

            Log.Info("Creating consumers");
            foreach (var consumerSettings in Settings.Consumers)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating consumer for {0}", consumerSettings.FormatIf(Log.IsInfoEnabled));
                var messageProcessor = new ConsumerInstancePool<byte[]>(consumerSettings, this, m => m);
                AddConsumer(consumerSettings, subscriber, messageProcessor);
            }

            if (Settings.RequestResponse != null)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating response consumer for {0}", Settings.RequestResponse.FormatIf(Log.IsInfoEnabled));
                var messageProcessor = new ResponseMessageProcessor<byte[]>(Settings.RequestResponse, this, m => m);
                AddConsumer(Settings.RequestResponse, subscriber, messageProcessor);
            }
        }

        protected void DestroyConsumers()
        {
            Log.Info("Destroying consumers");

            _consumers.ForEach(consumer => consumer.DisposeSilently("consumer", Log));
            _consumers.Clear();
        }

        protected void AddConsumer(AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            var consumer = new RedisChannelConsumer(consumerSettings, subscriber, messageProcessor);
            _consumers.Add(consumer);
        }

        public virtual Task Stop()
        {
            if (IsRunning)
            {
                DestroyConsumers();

                IsRunning = false;
            }
            return Task.CompletedTask;
        }

        #region Overrides of MessageBusBase

        public override async Task ProduceToTransport(Type messageType, object message, string name, byte[] payload)
        {
            var result = await Database.PublishAsync(name, payload).ConfigureAwait(false);
            Log.DebugFormat(CultureInfo.InvariantCulture, "Produced message {0} of type {1} to redis channel {2} with result {3}", message, messageType, name, result);
        }

        #endregion
    }
}