using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class EventHubConsumer : IDisposable, IEventProcessorFactory
    {
        private static readonly ILog Log = LogManager.GetLogger<EventHubConsumer>();

        public readonly EventHubMessageBus MessageBus;

        protected EventProcessorHost EventProcessorHost;
        protected readonly List<EventProcessor> EventProcessors = new List<EventProcessor>();
        protected readonly Func<EventHubConsumer, EventProcessor> EventProcessorFactory;

        public bool CanRun { get; protected set; } = true;

        public EventHubConsumer(EventHubMessageBus messageBus, ConsumerSettings consumerSettings)
            : this(messageBus, consumerSettings, x => new EventProcessorForConsumers(x, consumerSettings))
        {
        }

        public EventHubConsumer(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings)
            : this(messageBus, requestResponseSettings, x => new EventProcessorForResponses(x, requestResponseSettings))
        {
        }

        protected EventHubConsumer(EventHubMessageBus messageBus, ITopicGroupConsumerSettings consumerSettings, Func<EventHubConsumer, EventProcessor> eventProcessorFactory)
        {
            MessageBus = messageBus;
            EventProcessorFactory = eventProcessorFactory;

            Log.InfoFormat("Creating EventProcessorHost for Topic: {0}, Group: {1}", consumerSettings.Topic, consumerSettings.Group);
            EventProcessorHost = MessageBus.EventHubSettings.EventProcessorHostFactory(consumerSettings);
            var eventProcessorOptions = MessageBus.EventHubSettings.EventProcessorOptionsFactory(consumerSettings);
            EventProcessorHost.RegisterEventProcessorFactoryAsync(this, eventProcessorOptions).Wait();
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            CanRun = false;

            if (EventProcessorHost != null)
            {
                EventProcessorHost.UnregisterEventProcessorAsync().Wait();
                EventProcessorHost.Dispose();
                EventProcessorHost = null;
            }

            if (EventProcessors.Any())
            {
                EventProcessors.ForEach(ep => ep.DisposeSilently("EventProcessor", Log));
                EventProcessors.Clear();
            }
        }

        #endregion

        #region Implementation of IEventProcessorFactory

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            Log.DebugFormat("Creating EventHubEventProcessor for {0}", new PartitionContextInfo(context));
            var ep = EventProcessorFactory(this);
            EventProcessors.Add(ep);
            return ep;
        }

        #endregion
    }
}