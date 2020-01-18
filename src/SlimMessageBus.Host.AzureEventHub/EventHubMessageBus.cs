using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.Azure.EventHubs;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    /// <summary>
    /// MessageBus implementation for Azure Event Hub.
    /// </summary>
    public class EventHubMessageBus : MessageBusBase
    {
        private static readonly ILog Log = LogManager.GetLogger<EventHubMessageBus>();

        public EventHubMessageBusSettings ProviderSettings { get; }

        private SafeDictionaryWrapper<string, EventHubClient> _producerByTopic;
        private List<GroupTopicConsumer> _consumers = new List<GroupTopicConsumer>();

        public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings eventHubSettings)
            : base(settings)
        {
            ProviderSettings = eventHubSettings;

            OnBuildProvider();
        }

        #region Overrides of MessageBusBase

        protected override void Build()
        {
            base.Build();

            _producerByTopic = new SafeDictionaryWrapper<string, EventHubClient>(topic =>
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "Creating EventHubClient for path {0}", topic);
                return ProviderSettings.EventHubClientFactory(topic);
            });

            Log.Info("Creating consumers");
            foreach (var consumerSettings in Settings.Consumers)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating consumer for Topic: {0}, Group: {1}, MessageType: {2}", consumerSettings.Topic, consumerSettings.GetGroup(), consumerSettings.MessageType);
                _consumers.Add(new GroupTopicConsumer(this, consumerSettings));
            }

            if (Settings.RequestResponse != null)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating response consumer for Topic: {0}, Group: {1}", Settings.RequestResponse.Topic, Settings.RequestResponse.GetGroup());
                _consumers.Add(new GroupTopicConsumer(this, Settings.RequestResponse));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_consumers != null)
                {
                    _consumers.ForEach(c => c.DisposeSilently("Consumer", Log));
                    _consumers.Clear();
                }

                if (_producerByTopic != null)
                {
                    _producerByTopic.Clear(producer =>
                    {
                        Log.DebugFormat(CultureInfo.InvariantCulture, "Closing EventHubClient for path {0}", producer.EventHubName);
                        try
                        {
                            producer.Close();
                        }
                        catch (Exception e)
                        {
                            Log.ErrorFormat(CultureInfo.InvariantCulture, "Error while closing EventHubClient for path {0}", e, producer.EventHubName);
                        }
                    });
                }
            }
            base.Dispose(disposing);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="message"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public override async Task ProduceToTransport(Type messageType, object message, string name, byte[] payload)
        {
            AssertActive();

            Log.DebugFormat(CultureInfo.InvariantCulture, "Producing message {0} of type {1} on topic {2} with size {3}", message, messageType.Name, name, payload.Length);
            var producer = _producerByTopic.GetOrAdd(name);
            
            var ev = new EventData(payload);
            // ToDo: Add support for partition keys
            await producer.SendAsync(ev).ConfigureAwait(false);

            Log.DebugFormat(CultureInfo.InvariantCulture, "Delivered message {0} of type {1} on topic {2}", message, messageType.Name, name);
        }
    }
}
