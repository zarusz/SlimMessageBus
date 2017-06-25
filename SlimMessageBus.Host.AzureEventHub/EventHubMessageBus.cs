using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
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

        private readonly object _producerByTopicLock = new object();
        private IDictionary<string, EventHubClient> _producerByTopic = new Dictionary<string, EventHubClient>();

        private readonly List<EventHubConsumer> _consumers = new List<EventHubConsumer>();

        // ToDo: move to base class
        private bool _disposing = false;

        public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings eventHubSettings)
            : base(settings)
        {
            EventHubSettings = eventHubSettings;

            Log.Info("Creating consumers");
            foreach (var consumerSettings in settings.Consumers)
            {
                Log.InfoFormat("Creating consumer for Topic: {0}, Group: {1}, MessageType: {2}", consumerSettings.Topic, consumerSettings.Group, consumerSettings.MessageType);
                //var consumer = new EventHubConsumer(this, group, messageType, subscribersByMessageType.ToList());
                _consumers.Add(new EventHubConsumer(this, consumerSettings));
            }

            if (settings.RequestResponse != null)
            {
                Log.InfoFormat("Creating response consumer for Topic: {0}, Group: {1}", settings.RequestResponse.Topic, settings.RequestResponse.Group);
                // _consumers.Add(new EvenHubResponseConsumer(this, settings.RequestResponse));
                _consumers.Add(new EventHubConsumer(this, settings.RequestResponse));
            }
        }

        #region Overrides of MessageBusBase

        protected override void OnDispose()
        {
            _disposing = true;

            if (_producerByTopic.Any())
            {
                lock (_producerByTopicLock)
                {
                    foreach (var eventHubClient in _producerByTopic.Values)
                    {
                        Log.DebugFormat("Closing EventHubClient for path {0}", eventHubClient.Path);
                        try
                        {
                            eventHubClient.Close();
                        }
                        catch (Exception e)
                        {
                            Log.ErrorFormat("Error while closing EventHubClient for path {0}", e, eventHubClient.Path);
                        }
                    }
                    _producerByTopic.Clear();
                }
            }

            if (_consumers.Any())
            {
                _consumers.ForEach(c => c.DisposeSilently("Consumer", Log));
                _consumers.Clear();
            }

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
        public override async Task Publish(Type messageType, byte[] payload, string topic)
        {
            Assert.IsFalse(_disposing, () => new MessageBusException("The message bus is disposed at this time"));

            Log.DebugFormat("Producing message of type {0} on topic {1} with size {2}", messageType.Name, topic, payload.Length);
            var eventHubClient = GetOrCreateProducer(topic);

            var ev = new EventData(payload);
            await eventHubClient.SendAsync(ev);

            Log.DebugFormat("Delivered message at offset {0} and sequence {1}", ev.Offset, ev.SequenceNumber);
        }

        /// <summary>
        /// Create or obtain the <see cref="EventHubClient "/> for the given event hub name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private EventHubClient GetOrCreateProducer(string path)
        {
            EventHubClient eventHubClient;
            // check if we have the EventHubClient already for the HubName
            if (!_producerByTopic.TryGetValue(path, out eventHubClient))
            {
                lock (_producerByTopicLock)
                {
                    // double check if another thread did create it in meantime (before lock)
                    if (!_producerByTopic.TryGetValue(path, out eventHubClient))
                    {
                        Log.DebugFormat("Creating EventHubClient for path {0}", path);
                        eventHubClient = EventHubSettings.EventHubClientFactory(path);

                        _producerByTopic = new Dictionary<string, EventHubClient>(_producerByTopic)
                        {
                            {path, eventHubClient}
                        };
                    }
                }
            }
            return eventHubClient;
        }
    }
}
