using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        public EventHubMessageBusSettings EventHubSettings { get; }

        private readonly SafeDictionaryWrapper<string, EventHubClient> _producerByTopic;
        private readonly List<GroupTopicConsumer> _consumers = new List<GroupTopicConsumer>();

        public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings eventHubSettings)
            : base(settings)
        {
            EventHubSettings = eventHubSettings;

            _producerByTopic = new SafeDictionaryWrapper<string, EventHubClient>(topic =>
            {
                Log.DebugFormat(CultureInfo.InvariantCulture, "Creating EventHubClient for path {0}", topic);
                return EventHubSettings.EventHubClientFactory(topic);
            });

            Log.Info("Creating consumers");
            foreach (var consumerSettings in settings.Consumers)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating consumer for Topic: {0}, Group: {1}, MessageType: {2}", consumerSettings.Topic, consumerSettings.GetGroup(), consumerSettings.MessageType);
                _consumers.Add(new GroupTopicConsumer(this, consumerSettings));
            }

            if (settings.RequestResponse != null)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "Creating response consumer for Topic: {0}, Group: {1}", settings.RequestResponse.Topic, settings.RequestResponse.GetGroup());
                _consumers.Add(new GroupTopicConsumer(this, settings.RequestResponse));
            }
        }

        #region Overrides of MessageBusBase

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_consumers.Any())
                {
                    _consumers.ForEach(c => c.DisposeSilently("Consumer", Log));
                    _consumers.Clear();
                }

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
            base.Dispose(disposing);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="message"></param>
        /// <param name="topic"></param>
        /// <returns></returns>
        public override async Task PublishToTransport(Type messageType, object message, string topic, byte[] payload)
        {
            AssertActive();

            Log.DebugFormat(CultureInfo.InvariantCulture, "Producing message of type {0} on topic {1} with size {2}", messageType.Name, topic, payload.Length);
            var producer = _producerByTopic.GetOrAdd(topic);
            
            var ev = new EventData(payload);
            // ToDo: Add support for partition keys
            await producer.SendAsync(ev).ConfigureAwait(false);

            Log.DebugFormat(CultureInfo.InvariantCulture, "Delivered message with: {0}", ev.SystemProperties);
        }
    }
}
