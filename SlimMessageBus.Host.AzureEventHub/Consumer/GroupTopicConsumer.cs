using System;
using System.Collections.Generic;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class GroupTopicConsumer : IDisposable, IEventProcessorFactory
    {
        private static readonly ILog Log = LogManager.GetLogger<GroupTopicConsumer>();

        public readonly EventHubMessageBus MessageBus;

        private readonly EventProcessorHost _processorHost;
        private readonly Func<PartitionConsumer> _processorFactory;
        private readonly List<PartitionConsumer> _partitionConsumers = new List<PartitionConsumer>();

        private readonly TaskMarker _taskMarker = new TaskMarker();

        public GroupTopicConsumer(EventHubMessageBus messageBus, ConsumerSettings consumerSettings)
            : this(messageBus, consumerSettings, () => new PartitionConsumerForConsumers(messageBus, consumerSettings))
        {
        }

        public GroupTopicConsumer(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings)
            : this(messageBus, requestResponseSettings, () => new PartitionConsumerForResponses(messageBus, requestResponseSettings))
        {
        }

        protected GroupTopicConsumer(EventHubMessageBus messageBus, ITopicGroupConsumerSettings consumerSettings, Func<PartitionConsumer> processorFactory)
        {
            MessageBus = messageBus;
            _processorFactory = processorFactory;

            Log.InfoFormat("Creating EventProcessorHost for EventHub with Topic: {0}, Group: {1}", consumerSettings.Topic, consumerSettings.Group);
            _processorHost = MessageBus.EventHubSettings.EventProcessorHostFactory(consumerSettings);

            var eventProcessorOptions = MessageBus.EventHubSettings.EventProcessorOptionsFactory(consumerSettings);
            _processorHost.RegisterEventProcessorFactoryAsync(this, eventProcessorOptions).Wait();
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            _processorHost.UnregisterEventProcessorAsync().Wait();
            _processorHost.Dispose();

            _taskMarker.Stop().Wait();

            if (_partitionConsumers.Count > 0)
            {
                _partitionConsumers.ForEach(ep => ep.DisposeSilently("EventProcessor", Log));
                _partitionConsumers.Clear();
            }
        }

        #endregion

        #region Implementation of IEventProcessorFactory

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            Log.DebugFormat("Creating {0} for {1}", nameof(IEventProcessor), new PartitionContextInfo(context));
            var ep = _processorFactory();
            _partitionConsumers.Add(ep);
            return ep;
        }

        #endregion
    }
}