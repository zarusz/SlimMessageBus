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
        private readonly ILogger _logger;

        public ServiceBusMessageBusSettings ProviderSettings { get; }

        private SafeDictionaryWrapper<string, ITopicClient> _producerByTopic;
        private SafeDictionaryWrapper<string, IQueueClient> _producerByQueue;

        private readonly KindMapping _kindMapping = new KindMapping();

        private readonly List<BaseConsumer> _consumers = new List<BaseConsumer>();

        public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
            : base(settings)
        {
            _logger = LoggerFactory.CreateLogger<ServiceBusMessageBus>();
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

        protected void AddConsumer(AbstractConsumerSettings consumerSettings, IMessageProcessor<Message> messageProcessor)
        {
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

            var consumer = consumerSettings.PathKind == PathKind.Topic
                ? new TopicSubscriptionConsumer(this, consumerSettings, messageProcessor) as BaseConsumer
                : new QueueConsumer(this, consumerSettings, messageProcessor);

            _consumers.Add(consumer);
        }

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            _producerByTopic = new SafeDictionaryWrapper<string, ITopicClient>(topic =>
            {
                _logger.LogDebug("Creating {PathKind} client for path {Path}", PathKind.Topic, topic);
                return ProviderSettings.TopicClientFactory(topic);
            });

            _producerByQueue = new SafeDictionaryWrapper<string, IQueueClient>(queue =>
            {
                _logger.LogDebug("Creating {PathKind} client for path {Path}", PathKind.Queue, queue);
                return ProviderSettings.QueueClientFactory(queue);
            });

            _kindMapping.Configure(Settings);

            MessageWithHeaders messageProvider(Message m) => new MessageWithHeaders(m.Body, m.UserProperties);
            void initConsumerContext(Message m, ConsumerContext ctx) => ctx.SetTransportMessage(m);

            _logger.LogInformation("Creating consumers");
            foreach (var consumerSettings in Settings.Consumers)
            {
                _logger.LogInformation("Creating consumer for {0}", consumerSettings.FormatIf(_logger.IsEnabled(LogLevel.Information)));

                AddConsumer(consumerSettings, new ConsumerInstanceMessageProcessor<Message>(consumerSettings, this, messageProvider, initConsumerContext));
            }

            if (Settings.RequestResponse != null)
            {
                _logger.LogInformation("Creating response consumer for {0}", Settings.RequestResponse.FormatIf(_logger.IsEnabled(LogLevel.Information)));

                AddConsumer(Settings.RequestResponse, new ResponseMessageProcessor<Message>(Settings.RequestResponse, this, messageProvider));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_consumers.Count > 0)
            {
                _consumers.ForEach(c => c.DisposeSilently("Consumer", _logger));
                _consumers.Clear();
            }

            var disposeTasks = Enumerable.Empty<Task>();

            if (_producerByQueue.Dictonary.Count > 0)
            {
                disposeTasks = disposeTasks.Concat(_producerByQueue.Dictonary.Values.Select(x =>
                {
                    _logger.LogDebug("Closing {PathKind} client for path {Path}", PathKind.Queue, x.Path);
                    return x.CloseAsync();
                }));

                _producerByQueue.Clear();
            }

            if (_producerByTopic.Dictonary.Count > 0)
            {
                disposeTasks = disposeTasks.Concat(_producerByTopic.Dictonary.Values.Select(x =>
                {
                    _logger.LogDebug("Closing {PathKind} client for path {Path}", PathKind.Queue, x.Path);
                    return x.CloseAsync();
                }));

                _producerByTopic.Clear();
            }

            Task.WaitAll(disposeTasks.ToArray());

            base.Dispose(disposing);
        }

        protected virtual async Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, PathKind kind)
        {
            if (messageType is null) throw new ArgumentNullException(nameof(messageType));
            if (messagePayload is null) throw new ArgumentNullException(nameof(messagePayload));

            AssertActive();

            _logger.LogDebug("Producing message {Message} of type {MessageType} on {PathKind} {Path} with size {MessageSize}", message, messageType.Name, kind, path, messagePayload.Length);

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
#pragma warning disable CA1031 // Do not catch general exception types - Intended
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
                }
            }

            var senderClient = kind == PathKind.Topic 
                ? (ISenderClient)_producerByTopic.GetOrAdd(path) 
                : _producerByQueue.GetOrAdd(path);

            await senderClient.SendAsync(m).ConfigureAwait(false);

            _logger.LogDebug("Delivered message {Message} of type {MessageType} on {PathKind} {Path}", message, messageType.Name, kind, path);
        }

        public override Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders)
        {
            var kind = messageHeaders != null && messageHeaders.ContainsKey(MessageHeaderUseKind)
                ? (PathKind)messageHeaders.GetHeaderAsInt(MessageHeaderUseKind) // it was already determined what kind it is
                : _kindMapping.GetKind(messageType, path); // determine by path  or type if its a Azure SB queue or topic

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
