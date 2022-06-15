namespace SlimMessageBus.Host.Redis
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Collections;
    using SlimMessageBus.Host.Config;
    using StackExchange.Redis;

    public class RedisMessageBus : MessageBusBase
    {
        private readonly ILogger logger;

        public RedisMessageBusSettings ProviderSettings { get; }

        protected IConnectionMultiplexer Connection { get; private set; }
        protected IDatabase Database { get; private set; }

        private readonly KindMapping kindMapping = new KindMapping();

        private readonly List<IRedisConsumer> consumers = new List<IRedisConsumer>();

        public RedisMessageBus(MessageBusSettings settings, RedisMessageBusSettings providerSettings)
            : base(settings)
        {
            logger = LoggerFactory.CreateLogger<RedisMessageBus>();
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

            OnBuildProvider();
        }

        protected override void AssertSettings()
        {
            base.AssertSettings();

            Assert.IsNotNull(ProviderSettings.EnvelopeSerializer,
                () => new ConfigurationMessageBusException($"The {nameof(RedisMessageBusSettings)}.{nameof(RedisMessageBusSettings.EnvelopeSerializer)} is not set"));
        }

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            kindMapping.Configure(Settings);

            Connection = ProviderSettings.ConnectionFactory();
            Connection.ConnectionFailed += Connection_ConnectionFailed;
            Connection.ConnectionRestored += Connection_ConnectionRestored;
            Connection.ErrorMessage += Connection_ErrorMessage;
            Connection.ConfigurationChanged += Connection_ConfigurationChanged;
            Connection.ConfigurationChangedBroadcast += Connection_ConfigurationChangedBroadcast;

            Database = Connection.GetDatabase();

            try
            {
                ProviderSettings.OnDatabaseConnected?.Invoke(Database);
            }
            catch (Exception e)
            {
                // Do nothing
                logger.LogWarning(e, "Error occured while executing hook {0}", nameof(RedisMessageBusSettings.OnDatabaseConnected));
            }
        }

        private void Connection_ErrorMessage(object sender, RedisErrorEventArgs e)
        {
            logger.LogError("Redis recieved error message: {ErrorMessage}", e.Message);
        }

        private void Connection_ConfigurationChangedBroadcast(object sender, EndPointEventArgs e)
        {
            logger.LogDebug("Redis configuration changed broadcast from {EndPoint}", e.EndPoint);
        }

        private void Connection_ConfigurationChanged(object sender, EndPointEventArgs e)
        {
            logger.LogDebug("Redis configuration changed from {EndPoint}", e.EndPoint);
        }

        private void Connection_ConnectionRestored(object sender, ConnectionFailedEventArgs e)
        {
            logger.LogInformation("Redis connection restored - failure type {FailureType}, connection type: {ConnectionType}", e.FailureType, e.ConnectionType);
        }

        private void Connection_ConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            logger.LogError(e.Exception, "Redis connection failed - failure type {FailureType}, connection type: {ConnectionType}, with message {ErrorMessage}", e.FailureType, e.ConnectionType, e.Exception?.Message ?? "(empty)");
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore();

            Connection.DisposeSilently(nameof(ConnectionMultiplexer), logger);
        }

        #endregion

        protected override async Task OnStart()
        {
            await base.OnStart();

            CreateConsumers();

            foreach (var consumer in consumers)
            {
                await consumer.Start();
            }
        }

        protected override async Task OnStop()
        {
            await base.OnStop();

            foreach (var consumer in consumers)
            {
                await consumer.Stop();
            }

            DestroyConsumers();
        }

        protected void CreateConsumers()
        {
            var subscriber = Connection.GetSubscriber();

            var queues = new List<(string, IMessageProcessor<byte[]>)>();

            MessageWithHeaders MessageProvider(byte[] m) => (MessageWithHeaders)ProviderSettings.EnvelopeSerializer.Deserialize(typeof(MessageWithHeaders), m);

            logger.LogInformation("Creating consumers");
            foreach (var consumerSettings in Settings.Consumers)
            {
                IMessageProcessor<byte[]> processor = new ConsumerInstanceMessageProcessor<byte[]>(consumerSettings, this, MessageProvider);
                // When it was requested to have more than once concurrent instances working then we need to fan out the incoming Redis consumption tasks
                if (consumerSettings.Instances > 1)
                {
                    processor = new ConcurrencyIncreasingMessageProcessorDecorator<byte[]>(consumerSettings, this, processor);
                }

                if (consumerSettings.PathKind == PathKind.Topic)
                {
                    logger.LogInformation("Creating consumer {ConsumerType} for topic {Topic} and message type {MessageType}", consumerSettings.ConsumerType, consumerSettings.Path, consumerSettings.MessageType);
                    AddTopicConsumer(consumerSettings, subscriber, processor);
                }
                else
                {
                    logger.LogInformation("Creating consumer {ConsumerType} for queue {Queue} and message type {MessageType}", consumerSettings.ConsumerType, consumerSettings.Path, consumerSettings.MessageType);
                    queues.Add((consumerSettings.Path, processor));
                }
            }

            if (Settings.RequestResponse != null)
            {
                if (Settings.RequestResponse.PathKind == PathKind.Topic)
                {
                    logger.LogInformation("Creating response consumer for topic {Topic}", Settings.RequestResponse.Path);
                    AddTopicConsumer(Settings.RequestResponse, subscriber, new ResponseMessageProcessor<byte[]>(Settings.RequestResponse, this, MessageProvider));
                }
                else
                {
                    logger.LogInformation("Creating response consumer for queue {Queue}", Settings.RequestResponse.Path);
                    queues.Add((Settings.RequestResponse.Path, new ResponseMessageProcessor<byte[]>(Settings.RequestResponse, this, MessageProvider)));
                }
            }

            if (queues.Count > 0)
            {
                consumers.Add(new RedisListCheckerConsumer(LoggerFactory.CreateLogger<RedisListCheckerConsumer>(), Database, ProviderSettings.QueuePollDelay, ProviderSettings.QueuePollMaxIdle, queues));
            }
        }

        protected void DestroyConsumers()
        {
            logger.LogInformation("Destroying consumers");

            consumers.ForEach(consumer => consumer.DisposeSilently(nameof(RedisTopicConsumer), logger));
            consumers.Clear();
        }

        protected void AddTopicConsumer(AbstractConsumerSettings consumerSettings, ISubscriber subscriber, IMessageProcessor<byte[]> messageProcessor)
        {
            var consumer = new RedisTopicConsumer(LoggerFactory.CreateLogger<RedisTopicConsumer>(), consumerSettings, subscriber, messageProcessor);
            consumers.Add(consumer);
        }

        #region Overrides of MessageBusBase

        public override Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
        {
            var messageType = message.GetType();

            // determine the SMB topic name if its a Azure SB queue or topic
            var kind = kindMapping.GetKind(messageType, path);

            return ProduceToTransport(messageType, message, path, messagePayload, messageHeaders, cancellationToken, kind);
        }

        #endregion

        protected virtual async Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken, PathKind kind)
        {
            if (messageType is null) throw new ArgumentNullException(nameof(messageType));
            if (messagePayload is null) throw new ArgumentNullException(nameof(messagePayload));

            AssertActive();

            var messageWithHeaders = new MessageWithHeaders(messagePayload, messageHeaders);
            var messageWithHeadersBytes = ProviderSettings.EnvelopeSerializer.Serialize(typeof(MessageWithHeaders), messageWithHeaders);

            logger.LogDebug(
                kind == PathKind.Topic
                    ? "Producing message {Message} of type {MessageType} to redis channel {Topic} with size {MessageSize}"
                    : "Producing message {Message} of type {MessageType} to redis key {Queue} with size {MessageSize}",
                message, messageType.Name, path, messageWithHeadersBytes.Length);

            var result = kind == PathKind.Topic
                ? await Database.PublishAsync(path, messageWithHeadersBytes).ConfigureAwait(false) // Use Redis Pub/Sub
                : await Database.ListRightPushAsync(path, messageWithHeadersBytes).ConfigureAwait(false); // Use Redis List Type (append on the right side/end of list)

            logger.LogDebug(
                kind == PathKind.Topic
                    ? "Produced message {Message} of type {MessageType} to redis channel {Topic} with result {RedisResult}"
                    : "Produced message {Message} of type {MessageType} to redis key {Queue} with result {RedisResult}",
                message, messageType, path, result);
        }
    }
}