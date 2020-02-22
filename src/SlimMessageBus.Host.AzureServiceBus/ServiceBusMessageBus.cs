using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.AzureServiceBus.Consumer;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public class ServiceBusMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public ServiceBusMessageBusSettings ProviderSettings { get; }

        private SafeDictionaryWrapper<string, ITopicClient> _producerByTopic;
        private SafeDictionaryWrapper<string, IQueueClient> _producerByQueue;

        private readonly IDictionary<string, PathKind> _kindByTopic = new Dictionary<string, PathKind>();
        private readonly IDictionary<Type, PathKind> _kindByMessageType = new Dictionary<Type, PathKind>();

        private readonly List<BaseConsumer> _consumers = new List<BaseConsumer>();

        public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
            : base(settings)
        {
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
            OnBuildProvider();
        }

        protected void AddConsumer(AbstractConsumerSettings consumerSettings, IMessageProcessor<Message> messageProcessor)
        {
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

            var consumer = consumerSettings.GetKind() == PathKind.Topic
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
                Log.DebugFormat(CultureInfo.InvariantCulture, "Creating {0} for name {1}", nameof(ITopicClient), topic);
                return ProviderSettings.TopicClientFactory(topic);
            });

            _producerByQueue = new SafeDictionaryWrapper<string, IQueueClient>(queue =>
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "Creating {0} for name {1}", nameof(IQueueClient), queue);
                return ProviderSettings.QueueClientFactory(queue);
            });

            foreach (var producerSettings in Settings.Producers)
            {
                var producerKind = producerSettings.GetKind();
                PathKind existingKind;

                var topic = producerSettings.DefaultTopic;
                if (topic != null)
                {
                    if (_kindByTopic.TryGetValue(topic, out existingKind))
                    {
                        if (existingKind != producerKind)
                        {
                            throw new ConfigurationMessageBusException($"The same name '{topic}' was used for queue and topic. You cannot share one name for a topic and queue. Please fix your configuration.");
                        }
                    }
                    else
                    {
                        _kindByTopic.Add(topic, producerKind);
                    }
                }

                if (_kindByMessageType.TryGetValue(producerSettings.MessageType, out existingKind))
                {
                    if (existingKind != producerKind)
                    {
                        throw new ConfigurationMessageBusException($"The same message type '{producerSettings.MessageType}' was used for queue and topic. You cannot share one message type for a topic and queue. Please fix your configuration.");
                    }
                }
                else
                {
                    _kindByMessageType.Add(producerSettings.MessageType, producerKind);
                }
            }

            byte[] getPayload(Message m) => m.Body;

            Log.Info("Creating consumers");
            foreach (var consumerSettings in Settings.Consumers)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating consumer for {0}", consumerSettings.FormatIf(Log.IsInfoEnabled));

                var messageProcessor = new ConsumerInstancePoolMessageProcessor<Message>(consumerSettings, this, getPayload);
                AddConsumer(consumerSettings, messageProcessor);
            }

            if (Settings.RequestResponse != null)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating response consumer for {0}", Settings.RequestResponse.FormatIf(Log.IsInfoEnabled));

                var messageProcessor = new ResponseMessageProcessor<Message>(Settings.RequestResponse, this, getPayload);
                AddConsumer(Settings.RequestResponse, messageProcessor);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_consumers.Count > 0)
            {
                _consumers.ForEach(c => c.DisposeSilently("Consumer", Log));
                _consumers.Clear();
            }

            var disposeTasks = Enumerable.Empty<Task>();

            if (_producerByQueue.Dictonary.Count > 0)
            {
                disposeTasks = disposeTasks.Concat(_producerByQueue.Dictonary.Values.Select(x =>
                {
                    Log.DebugFormat(CultureInfo.InvariantCulture, "Closing {0} for name {1}", nameof(IQueueClient), x.Path);
                    return x.CloseAsync();
                }));

                _producerByQueue.Clear();
            }

            if (_producerByTopic.Dictonary.Count > 0)
            {
                disposeTasks = disposeTasks.Concat(_producerByTopic.Dictonary.Values.Select(x =>
                {
                    Log.DebugFormat(CultureInfo.InvariantCulture, "Closing {0} for name {1}", nameof(ITopicClient), x.Path);
                    return x.CloseAsync();
                }));

                _producerByTopic.Clear();
            }

            Task.WaitAll(disposeTasks.ToArray());

            base.Dispose(disposing);
        }

        protected virtual async Task ProduceToTransport(Type messageType, object message, string name, byte[] payload, PathKind kind)
        {
            if (messageType is null) throw new ArgumentNullException(nameof(messageType));
            if (payload is null) throw new ArgumentNullException(nameof(payload));

            AssertActive();

            Log.DebugFormat(CultureInfo.InvariantCulture, "Producing message {0} of type {1} on {2} {3} with size {4}", message, messageType.Name, kind, name, payload.Length);

            var m = new Message(payload);

            if (ProducerSettingsByMessageType.TryGetValue(messageType, out var producerSettings))
            {
                try
                {
                    var messageModifier = producerSettings.GetMessageModifier();
                    messageModifier(message, m);
                }
                catch (Exception e)
                {
                    Log.WarnFormat(CultureInfo.InvariantCulture, "The configured message modifier failed for message type {0} and message {1}", e, messageType, message);
                }
            }

            if (kind == PathKind.Topic)
            {
                var topicProducer = _producerByTopic.GetOrAdd(name);
                await topicProducer.SendAsync(m).ConfigureAwait(false);
            }
            else
            {
                var queueProducer = _producerByQueue.GetOrAdd(name);
                await queueProducer.SendAsync(m).ConfigureAwait(false);
            }

            Log.DebugFormat(CultureInfo.InvariantCulture, "Delivered message {0} of type {1} on {2} {3}", message, messageType.Name, kind, name);
        }

        public override Task ProduceToTransport(Type messageType, object message, string name, byte[] payload, MessageWithHeaders messageWithHeaders = null)
        {
            // determine the SMB topic name if its a Azure SB queue or topic
            if (!_kindByTopic.TryGetValue(name, out var kind))
            {
                if (!_kindByMessageType.TryGetValue(messageType, out kind))
                {
                    // by default this will be a topic
                    kind = PathKind.Topic;
                }
            }

            return ProduceToTransport(messageType, message, name, payload, kind);
        }

        public static readonly string RequestHeaderReplyToKind = "reply-to-kind";

        public override Task ProduceRequest(object request, MessageWithHeaders requestMessage, string name, ProducerSettings producerSettings)
        {
            if (requestMessage is null) throw new ArgumentNullException(nameof(requestMessage));

            requestMessage.SetHeader(RequestHeaderReplyToKind, (int)Settings.RequestResponse.GetKind());
            return base.ProduceRequest(request, requestMessage, name, producerSettings);
        }

        public override Task ProduceResponse(object request, MessageWithHeaders requestMessage, object response, MessageWithHeaders responseMessage, ConsumerSettings consumerSettings)
        {
            if (requestMessage is null) throw new ArgumentNullException(nameof(requestMessage));
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

            var replyTo = requestMessage.Headers[ReqRespMessageHeaders.ReplyTo];
            var kind = (PathKind)requestMessage.GetHeaderAsInt(RequestHeaderReplyToKind);

            var responseMessagePayload = SerializeResponse(consumerSettings.ResponseType, response, responseMessage);

            return ProduceToTransport(consumerSettings.ResponseType, response, replyTo, responseMessagePayload, kind);
        }

        #endregion
    }
}
