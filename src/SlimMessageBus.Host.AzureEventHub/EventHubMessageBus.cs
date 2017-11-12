using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
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
                Log.DebugFormat("Creating EventHubClient for path {0}", topic);
                return EventHubSettings.EventHubClientFactory(topic);
            });

            Log.Info("Creating consumers");
            foreach (var consumerSettings in settings.Consumers)
            {
                Log.InfoFormat("Creating consumer for Topic: {0}, Group: {1}, MessageType: {2}", consumerSettings.Topic, consumerSettings.Group, consumerSettings.MessageType);
                _consumers.Add(new GroupTopicConsumer(this, consumerSettings));
            }

            if (settings.RequestResponse != null)
            {
                Log.InfoFormat("Creating response consumer for Topic: {0}, Group: {1}", settings.RequestResponse.Topic, settings.RequestResponse.Group);
                _consumers.Add(new GroupTopicConsumer(this, settings.RequestResponse));
            }
        }

        #region Overrides of MessageBusBase

        protected override void OnDispose()
        {
            if (_consumers.Any())
            {
                _consumers.ForEach(c => c.DisposeSilently("Consumer", Log));
                _consumers.Clear();
            }

            _producerByTopic.Clear(producer =>
            {
                Log.DebugFormat("Closing EventHubClient for path {0}", producer.Path);
                try
                {
                    producer.Close();
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Error while closing EventHubClient for path {0}", e, producer.Path);
                }
            });

            base.OnDispose();
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="payload"></param>
        /// <param name="topic"></param>
        /// <returns></returns>
        public override async Task PublishToTransport(Type messageType, object message, string topic, byte[] payload)
        {
            AssertActive();

            Log.DebugFormat("Producing message of type {0} on topic {1} with size {2}", messageType.Name, topic, payload.Length);
            var producer = _producerByTopic.GetOrAdd(topic);
            
            var ev = new EventData(payload);
            await producer.SendAsync(ev);

            Log.DebugFormat("Delivered message at offset {0} and sequence {1}", ev.Offset, ev.SequenceNumber);
        }
    }
}
