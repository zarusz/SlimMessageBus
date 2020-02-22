using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Memory
{
    /// <summary>
    /// In memory message bus <see cref="IMessageBus"/> implementation to use for in process message passing.
    /// </summary>
    public class MemoryMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<MemoryMessageBus>();

        private MemoryMessageBusSettings ProviderSettings { get; }

        private IDictionary<string, List<ConsumerSettings>> _consumersByTopic;

        public MemoryMessageBus(MessageBusSettings settings, MemoryMessageBusSettings providerSettings)
            : base(settings)
        {
            ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
            OnBuildProvider();
        }

        #region Overrides of MessageBusBase

        protected override void AssertSerializerSettings()
        {
            if (ProviderSettings.EnableMessageSerialization)
            {
                base.AssertSerializerSettings();
            }
        }

        protected override void Build()
        {
            base.Build();

            _consumersByTopic = Settings.Consumers
                .GroupBy(x => x.Topic)
                .ToDictionary(x => x.Key, x => x.ToList());
        }

        public override Task ProduceToTransport(Type messageType, object message, string name, byte[] messagePayload, MessageWithHeaders messageWithHeaders = null)
        {
            if (!_consumersByTopic.TryGetValue(name, out var consumers))
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "No consumers interested in message type {0} on topic {1}", messageType, name);
                return Task.CompletedTask;
            }

            var tasks = new LinkedList<Task>();
            foreach (var consumer in consumers)
            {
                // obtain the consumer from DI
                Log.DebugFormat(CultureInfo.InvariantCulture, "Resolving consumer type {0}", consumer.ConsumerType);
                var consumerInstance = Settings.DependencyResolver.Resolve(consumer.ConsumerType);
                if (consumerInstance == null)
                {
                    Log.WarnFormat(CultureInfo.InvariantCulture, "The dependency resolver did not yield any instance of {0}", consumer.ConsumerType);
                    continue;
                }

                var messageForConsumer = ProviderSettings.EnableMessageSerialization
                    ? DeserializeMessage(messageType, messagePayload) // will pass a deep copy of the message
                    : message; // prevent deep copy of the message

                Log.DebugFormat(CultureInfo.InvariantCulture, "Invoking {0} {1}", consumer.ConsumerMode == ConsumerMode.Consumer ? "consumer" : "handler", consumerInstance.GetType());
                var task = consumer.ConsumerMethod(consumerInstance, messageForConsumer, consumer.Topic);

                if (consumer.ConsumerMode == ConsumerMode.RequestResponse)
                {
                    var requestId = messageWithHeaders.Headers[ReqRespMessageHeaders.RequestId];

                    task = task.ContinueWith(x =>
                    {
                        if (x.IsFaulted || x.IsCanceled)
                        {
                            return OnResponseArrived(null, name, requestId, x.IsCanceled ? "Cancelled" : x.Exception.Message, null);
                        }

                        var response = consumer.ConsumerMethodResult(x);
                        var responsePayload = SerializeMessage(consumer.ResponseType, response);

                        return OnResponseArrived(responsePayload, name, requestId, null, response);

                    }, TaskScheduler.Current).Unwrap();
                }

                tasks.AddLast(task);
            }

            Log.DebugFormat(CultureInfo.InvariantCulture, "Waiting on {0} consumer tasks", tasks.Count);
            return Task.WhenAll(tasks);
        }

        public override byte[] SerializeMessage(Type messageType, object message)
        {
            if (!ProviderSettings.EnableMessageSerialization)
            {
                // the serialized payload is not going to be used
                return null;
            }

            return base.SerializeMessage(messageType, message);
        }

        public override byte[] SerializeRequest(Type requestType, object request, MessageWithHeaders requestMessage, ProducerSettings producerSettings)
        {
            if (!ProviderSettings.EnableMessageSerialization)
            {
                // the serialized payload is not going to be used
                return null;
            }

            return base.SerializeRequest(requestType, request, requestMessage, producerSettings);
        }

        public override byte[] SerializeResponse(Type responseType, object response, MessageWithHeaders responseMessage)
        {
            if (!ProviderSettings.EnableMessageSerialization)
            {
                // the serialized payload is not going to be used
                return null;
            }

            return base.SerializeResponse(responseType, response, responseMessage);
        }

        #endregion
    }
}
