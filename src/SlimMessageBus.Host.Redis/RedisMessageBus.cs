namespace SlimMessageBus.Host.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Collections;
    using SlimMessageBus.Host.Config;
    using StackExchange.Redis;

    public class RedisMessageBus : MessageBusBase
    {
        private readonly ILogger _logger;

        public RedisMessageBusSettings ProviderSettings { get; }

        public bool IsRunning { get; private set; }

        protected IConnectionMultiplexer Connection { get; private set; }
        protected IDatabase Database { get; private set; }

        private readonly KindMapping _kindMapping = new KindMapping();

        private readonly List<IRedisConsumer> _consumers = new List<IRedisConsumer>();

        public RedisMessageBus(MessageBusSettings settings, RedisMessageBusSettings providerSettings)
            : base(settings)
        {
            _logger = LoggerFactory.CreateLogger<RedisMessageBus>();
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
            OnBuildProvider();
        }

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            Connection = ProviderSettings.ConnectionFactory();
            Database = Connection.GetDatabase();

            _kindMapping.Configure(Settings);

            if (ProviderSettings.AutoStartConsumers)
            {
                _ = Start();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (IsRunning)
                {
                    Finish().Wait();
                }
                Connection.DisposeSilently(nameof(ConnectionMultiplexer), _logger);
            }
        }

        #endregion

        // ToDo: lift to base class
        public virtual async Task Start()
        {
            if (!IsRunning)
            {
                IsRunning = true;

                CreateConsumers();

                foreach (var consumer in _consumers)
                {
                    await consumer.Start().ConfigureAwait(false);
                }
            }
        }

        // ToDo: lift to base class
        public virtual async Task Finish()
        {
            if (IsRunning)
            {
                foreach (var consumer in _consumers)
                {
                    await consumer.Finish().ConfigureAwait(false);
                }

                DestroyConsumers();

                IsRunning = false;
            }
        }

        protected void CreateConsumers()
        {
            var subscriber = Connection.GetSubscriber();

            var queues = new List<(string, IMessageProcessor<byte[]>)>();

            _logger.LogInformation("Creating consumers");
            foreach (var consumerSettings in Settings.Consumers)
            {
                var processor = new ConsumerInstancePoolMessageProcessor<byte[]>(consumerSettings, this, m => m);

                if (consumerSettings.PathKind == PathKind.Topic)
                {
                    _logger.LogInformation("Creating consumer {ConsumerType} for topic {Topic} and message type {MessageType}", consumerSettings.ConsumerType, consumerSettings.Path, consumerSettings.MessageType);
                    AddTopicConsumer(consumerSettings, subscriber, processor);
                }
                else
                {
                    _logger.LogInformation("Creating consumer {ConsumerType} for queue {Queue} and message type {MessageType}", consumerSettings.ConsumerType, consumerSettings.Path, consumerSettings.MessageType);
                    queues.Add((consumerSettings.Path, processor));
                }
            }

            if (Settings.RequestResponse != null)
            {
                if (Settings.RequestResponse.PathKind == PathKind.Topic)
                {
                    _logger.LogInformation("Creating response consumer for topic {Topic}", Settings.RequestResponse.Path);
                    AddTopicConsumer(Settings.RequestResponse, subscriber, new ResponseMessageProcessor<byte[]>(Settings.RequestResponse, this, m => m));
                }
                else
                {
                    _logger.LogInformation("Creating response consumer for queue {Queue}", Settings.RequestResponse.Path);
                    queues.Add((Settings.RequestResponse.Path, new ResponseMessageProcessor<byte[]>(Settings.RequestResponse, this, m => m)));
                }
            }

            if (queues.Count > 0)
            {
                _consumers.Add(new RedisListCheckerConsumer(LoggerFactory.CreateLogger<RedisListCheckerConsumer>(), Database, ProviderSettings.QueuePollDelay, ProviderSettings.QueuePollMaxIdle, queues));
            }
        }

        protected void DestroyConsumers()
        {
            _logger.LogInformation("Destroying consumers");

            _consumers.ForEach(consumer => consumer.DisposeSilently(nameof(RedisTopicConsumer), _logger));
            _consumers.Clear();
        }

        protected void AddTopicConsumer(AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            var consumer = new RedisTopicConsumer(consumerSettings, subscriber, messageProcessor);
            _consumers.Add(consumer);
        }

        #region Overrides of MessageBusBase

        public override Task ProduceToTransport(Type messageType, object message, string path, byte[] payload, MessageWithHeaders messageWithHeaders = null)
        {
            // determine the SMB topic name if its a Azure SB queue or topic
            var kind = _kindMapping.GetKind(messageType, path);

            return ProduceToTransport(messageType, message, path, payload, kind);
        }

        #endregion

        protected virtual async Task ProduceToTransport(Type messageType, object message, string path, byte[] payload, PathKind kind)
        {
            if (messageType is null) throw new ArgumentNullException(nameof(messageType));
            if (payload is null) throw new ArgumentNullException(nameof(payload));

            AssertActive();

            _logger.LogDebug(
                kind == PathKind.Topic
                    ? "Producing message {Message} of type {MessageType} to redis channel {Topic} with size {MessageSize}"
                    : "Producing message {Message} of type {MessageType} to redis key {Queue} with size {MessageSize}",
                message, messageType.Name, path, payload.Length);

            var result = kind == PathKind.Topic
                ? await Database.PublishAsync(path, payload).ConfigureAwait(false) // Use Redis Pub/Sub
                : await Database.ListRightPushAsync(path, payload).ConfigureAwait(false); // Use Redis List Type (append on the right side/end of list)

            _logger.LogDebug(
                kind == PathKind.Topic
                    ? "Produced message {Message} of type {MessageType} to redis channel {Topic} with result {RedisResult}"
                    : "Produced message {Message} of type {MessageType} to redis key {Queue} with result {RedisResult}",
                message, messageType, path, result);
        }
    }
}