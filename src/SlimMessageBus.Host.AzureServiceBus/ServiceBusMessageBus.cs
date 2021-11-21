namespace SlimMessageBus.Host.AzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.AzureServiceBus.Consumer;
    using SlimMessageBus.Host.Collections;
    using SlimMessageBus.Host.Config;

    public class ServiceBusMessageBus : MessageBusBase
    {
        private readonly ILogger logger;

        public ServiceBusMessageBusSettings ProviderSettings { get; }

        private SafeDictionaryWrapper<string, ITopicClient> producerByTopic;
        private SafeDictionaryWrapper<string, IQueueClient> producerByQueue;

        private readonly KindMapping kindMapping = new KindMapping();

        private readonly List<BaseConsumer> consumers = new List<BaseConsumer>();

        public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
            : base(settings)
        {
            logger = LoggerFactory.CreateLogger<ServiceBusMessageBus>();
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

            OnBuildProvider();
        }

        protected override void AssertConsumerSettings(ConsumerSettings consumerSettings)
        {
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

            base.AssertConsumerSettings(consumerSettings);

            Assert.IsTrue(consumerSettings.PathKind != PathKind.Topic || consumerSettings.GetSubscriptionName(required: false) != null,
                () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(SettingsExtensions.SubscriptionName)} is not set on topic {consumerSettings.Path}"));
        }

        protected void AddConsumer(string path, PathKind pathKind, string subscriptionName, IEnumerable<IMessageProcessor<Message>> consumers)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            if (consumers is null) throw new ArgumentNullException(nameof(consumers));

            BaseConsumer consumer;

            if (pathKind == PathKind.Topic)
            {
                logger.LogInformation("Creating consumer for {PathKind} Path: {Path}, SubscriptionName: {SubscriptionName}", pathKind, path, subscriptionName);
                consumer = new TopicSubscriptionConsumer(this, consumers, path, subscriptionName);
            }
            else
            {
                logger.LogInformation("Creating consumer for {PathKind} Path: {Path}", pathKind, path);
                consumer = new QueueConsumer(this, consumers, path);
            }

            this.consumers.Add(consumer);
        }

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            producerByTopic = new SafeDictionaryWrapper<string, ITopicClient>(path =>
            {
                logger.LogDebug("Creating {PathKind} client for path {Path}", PathKind.Topic, path);
                return ProviderSettings.TopicClientFactory(path);
            });

            producerByQueue = new SafeDictionaryWrapper<string, IQueueClient>(path =>
            {
                logger.LogDebug("Creating {PathKind} client for path {Path}", PathKind.Queue, path);
                return ProviderSettings.QueueClientFactory(path);
            });

            kindMapping.Configure(Settings);

            static MessageWithHeaders messageProvider(Message m) => new MessageWithHeaders(m.Body, m.UserProperties);
            static void initConsumerContext(Message m, ConsumerContext ctx) => ctx.SetTransportMessage(m);

            logger.LogInformation("Creating consumers");

            foreach (var consumerSettingsByPath in Settings.Consumers.GroupBy(x => (x.Path, x.PathKind, SubscriptionName: x.GetSubscriptionName(required: false))))
            {
                var key = consumerSettingsByPath.Key;

                var consumers = consumerSettingsByPath.Select(x => new ConsumerInstanceMessageProcessor<Message>(x, this, messageProvider, initConsumerContext)).ToList();
                AddConsumer(key.Path, key.PathKind, key.SubscriptionName, consumers);
            }

            if (Settings.RequestResponse != null)
            {
                var (path, pathKind, subscriptionName) = (Settings.RequestResponse.Path, Settings.RequestResponse.PathKind, Settings.RequestResponse.GetSubscriptionName(required: false));

                var consumers = new[]
                {
                    new ResponseMessageProcessor<Message>(Settings.RequestResponse, this, messageProvider)
                };
                AddConsumer(path, pathKind, subscriptionName, consumers);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (consumers.Count > 0)
            {
                consumers.ForEach(c => c.DisposeSilently("Consumer", logger));
                consumers.Clear();
            }

            var disposeTasks = Enumerable.Empty<Task>();

            if (producerByQueue.Dictonary.Count > 0)
            {
                disposeTasks = disposeTasks.Concat(producerByQueue.Dictonary.Values.Select(x =>
                {
                    logger.LogDebug("Closing {PathKind} client for path {Path}", PathKind.Queue, x.Path);
                    return x.CloseAsync();
                }));

                producerByQueue.Clear();
            }

            if (producerByTopic.Dictonary.Count > 0)
            {
                disposeTasks = disposeTasks.Concat(producerByTopic.Dictonary.Values.Select(x =>
                {
                    logger.LogDebug("Closing {PathKind} client for path {Path}", PathKind.Topic, x.Path);
                    return x.CloseAsync();
                }));

                producerByTopic.Clear();
            }

            Task.WaitAll(disposeTasks.ToArray());

            base.Dispose(disposing);
        }

        protected virtual async Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, PathKind kind)
        {
            if (messageType is null) throw new ArgumentNullException(nameof(messageType));
            if (messagePayload is null) throw new ArgumentNullException(nameof(messagePayload));

            AssertActive();

            logger.LogDebug("Producing message {Message} of type {MessageType} on {PathKind} {Path} with size {MessageSize}", message, messageType.Name, kind, path, messagePayload.Length);

            var m = new Message(messagePayload);
            if (messageHeaders != null)
            {
                foreach (var header in messageHeaders)
                {
                    if (header.Key != MessageHeaderUseKind) // Skip special header that is used to convey routing information via headers.
                    {
                        m.UserProperties.Add(header.Key, header.Value);
                    }
                }
            }

            if (ProducerSettingsByMessageType.TryGetValue(messageType, out var producerSettings))
            {
                try
                {
                    var messageModifier = producerSettings.GetMessageModifier();
                    messageModifier?.Invoke(message, m);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
                }
            }

            var senderClient = kind == PathKind.Topic
                ? (ISenderClient)producerByTopic.GetOrAdd(path)
                : producerByQueue.GetOrAdd(path);

            await senderClient.SendAsync(m).ConfigureAwait(false);

            logger.LogDebug("Delivered message {Message} of type {MessageType} on {PathKind} {Path}", message, messageType.Name, kind, path);
        }

        public override Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders)
        {
            var kind = messageHeaders != null && messageHeaders.ContainsKey(MessageHeaderUseKind)
                ? (PathKind)messageHeaders.GetHeaderAsInt(MessageHeaderUseKind) // it was already determined what kind it is
                : kindMapping.GetKind(messageType, path); // determine by path  or type if its a Azure SB queue or topic

            return ProduceToTransport(messageType, message, path, messagePayload, messageHeaders, kind);
        }

        public static readonly string RequestHeaderReplyToKind = "reply-to-kind";
        public static readonly string MessageHeaderUseKind = "smb-use-kind";

        public override Task ProduceRequest(object request, IDictionary<string, object> headers, string path, ProducerSettings producerSettings)
        {
            if (headers is null) throw new ArgumentNullException(nameof(headers));

            headers.SetHeader(RequestHeaderReplyToKind, (int)Settings.RequestResponse.PathKind);
            return base.ProduceRequest(request, headers, path, producerSettings);
        }

        public override Task ProduceResponse(object request, IDictionary<string, object> requestHeaders, object response, IDictionary<string, object> responseHeaders, ConsumerSettings consumerSettings)
        {
            if (requestHeaders is null) throw new ArgumentNullException(nameof(requestHeaders));
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

            var kind = (PathKind)requestHeaders[RequestHeaderReplyToKind];
            responseHeaders.SetHeader(MessageHeaderUseKind, (int)kind);

            return base.ProduceResponse(consumerSettings.ResponseType, requestHeaders, response, responseHeaders, consumerSettings);
        }

        #endregion
    }
}
