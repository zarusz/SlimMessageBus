using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        private readonly EventHubMessageBusSettings _eventHubSettings;

        private readonly object _eventHubClientByTopicLock = new object();
        private IDictionary<string, EventHubClient> _eventHubClientByTopic = new Dictionary<string, EventHubClient>();

        // ToDo: move to base class
        private bool _disposing = false;

        public EventHubMessageBus(MessageBusSettings settings, EventHubMessageBusSettings eventHubSettings)
            : base(settings)
        {
            _eventHubSettings = eventHubSettings;
        }

        #region Overrides of MessageBusBase

        protected override void OnDispose()
        {
            _disposing = true;

            lock (_eventHubClientByTopicLock)
            {
                foreach (var eventHubClient in _eventHubClientByTopic.Values)
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
                _eventHubClientByTopic.Clear();                
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
            var eventHubClient = GetOrCreateEventHubClient(topic);
            await eventHubClient.SendAsync(new EventData(payload));
            Log.DebugFormat("Delivered message");
        }

        /// <summary>
        /// Create or obtain the <see cref="EventHubClient "/> for the given event hub name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private EventHubClient GetOrCreateEventHubClient(string path)
        {
            EventHubClient eventHubClient;
            // check if we have the EventHubClient already for the HubName
            if (!_eventHubClientByTopic.TryGetValue(path, out eventHubClient))
            {
                lock (_eventHubClientByTopicLock)
                {
                    // double check if another thread did create it in meantime (before lock)
                    if (!_eventHubClientByTopic.TryGetValue(path, out eventHubClient))
                    {
                        Log.DebugFormat("Creating EventHubClient for path {0}", path);
                        eventHubClient = _eventHubSettings.EventHubClientFactory(path);

                        _eventHubClientByTopic = new Dictionary<string, EventHubClient>(_eventHubClientByTopic)
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
