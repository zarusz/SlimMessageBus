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

        public RedisMessageBusSettings ProviderSettings { get; }

        public bool IsRunning { get; private set; } = false;

        protected ConnectionMultiplexer Connection { get; private set; }
        protected IDatabase Database { get; private set; }

        private readonly List<RedisChannelConsumer> _consumers = new List<RedisChannelConsumer>();

        public RedisMessageBus(MessageBusSettings settings, RedisMessageBusSettings providerSettings)
            : base(settings)
        {
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
            OnBuildProvider();
        }

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            Connection = ProviderSettings.ConnectionFactory();
            Database = Connection.GetDatabase();

            if (ProviderSettings.AutoStartConsumers)
            {
                Start().Wait();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (IsRunning)
                {
                    Stop().Wait();
                }
                Connection.DisposeSilently(nameof(ConnectionMultiplexer), Log);
            }
        }

        #endregion

        // ToDo: lift to base class
        public virtual Task Start()
        {
            if (!IsRunning)
            {
                IsRunning = true;

                CreateConsumers();
            }
            return Task.CompletedTask;
        }

        // ToDo: lift to base class
        public virtual Task Stop()
        {
            if (IsRunning)
            {
                DestroyConsumers();

                IsRunning = false;
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
                var messageProcessor = new ConsumerInstancePoolMessageProcessor<byte[]>(consumerSettings, this, m => m);
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

            _consumers.ForEach(consumer => consumer.DisposeSilently(nameof(RedisChannelConsumer), Log));
            _consumers.Clear();
        }

        protected void AddConsumer(AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            var consumer = new RedisChannelConsumer(consumerSettings, subscriber, messageProcessor);
            _consumers.Add(consumer);
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