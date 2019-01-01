using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private static readonly ILog Log = LogManager.GetLogger<ServiceBusMessageBus>();

        public ServiceBusMessageBusSettings ServiceBusSettings { get; }

        private readonly SafeDictionaryWrapper<string, TopicClient> _producerByTopic;
        private readonly List<TopicSubscriptionConsumer> _consumers = new List<TopicSubscriptionConsumer>();

        public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings serviceBusSettings) : base(settings)
        {
            ServiceBusSettings = serviceBusSettings;

            _producerByTopic = new SafeDictionaryWrapper<string, TopicClient>(topic =>
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "Creating TopicClient for path {0}", topic);
                return serviceBusSettings.TopicClientFactory(new TopicClientFactoryParams(topic));
            });

            Log.Info("Creating consumers");
            foreach (var consumerSettings in settings.Consumers)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating consumer for {0}", consumerSettings.FormatIf(Log.IsInfoEnabled));
                _consumers.Add(new TopicSubscriptionConsumer(this, consumerSettings));
            }
        }

        #region Overrides of MessageBusBase

        protected override void Dispose(bool disposing)
        {
            if (_consumers.Count > 0)
            {
                _consumers.ForEach(c => c.DisposeSilently("Consumer", Log));
                _consumers.Clear();
            }

            if (_producerByTopic.Dictonary.Count > 0)
            {
                Task.WaitAll(_producerByTopic.Dictonary.Values.Select(x =>
                {
                    Log.DebugFormat(CultureInfo.InvariantCulture, "Closing TopicClient for Topic {0}", x.Path);
                    return x.CloseAsync();
                }).ToArray());

                _producerByTopic.Clear();
            }

            base.Dispose(disposing);
        }

        public override async Task PublishToTransport(Type messageType, object message, string topic, byte[] payload)
        {
            AssertActive();

            Log.DebugFormat(CultureInfo.InvariantCulture, "Producing message {0} of type {1} on topic {2} with size {3}", message, messageType.Name, topic, payload.Length);
            var producer = _producerByTopic.GetOrAdd(topic);

            var m = new Message(payload);
            // ToDo: add support for partitioning key
            await producer.SendAsync(m).ConfigureAwait(false);

            Log.DebugFormat(CultureInfo.InvariantCulture, "Delivered message {0} of type {1} on topic {2}", message, messageType.Name, topic);
        }

        #endregion
    }
}
