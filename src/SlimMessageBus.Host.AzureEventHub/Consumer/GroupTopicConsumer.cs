using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class GroupTopicConsumer : IDisposable, IEventProcessorFactory
    {
        private readonly ILogger _logger;

        public EventHubMessageBus MessageBus { get; }

        private readonly EventProcessorHost _processorHost;
        private readonly Func<PartitionConsumer> _partitionConsumerFactory;
        private readonly List<PartitionConsumer> _partitionConsumers = new List<PartitionConsumer>();

        private readonly TaskMarker _taskMarker = new TaskMarker();

        public GroupTopicConsumer(EventHubMessageBus messageBus, ConsumerSettings consumerSettings)
            : this(messageBus, new TopicGroup(consumerSettings.Topic, consumerSettings.GetGroup()), () => new PartitionConsumerForConsumers(messageBus, consumerSettings))
        {            
        }

        public GroupTopicConsumer(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings)
            : this(messageBus, new TopicGroup(requestResponseSettings.Topic, requestResponseSettings.GetGroup()), () => new PartitionConsumerForResponses(messageBus, requestResponseSettings))
        {
        }

        protected GroupTopicConsumer(EventHubMessageBus messageBus, TopicGroup topicGroup, Func<PartitionConsumer> partitionConsumerFactory)
        {
            if (topicGroup is null) throw new ArgumentNullException(nameof(topicGroup));

            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _logger = messageBus.LoggerFactory.CreateLogger<GroupTopicConsumer>();
            _partitionConsumerFactory = partitionConsumerFactory ?? throw new ArgumentNullException(nameof(partitionConsumerFactory));

            _logger.LogInformation("Creating EventProcessorHost for EventHub with Topic: {0}, Group: {1}", topicGroup.Topic, topicGroup.Group);
            _processorHost = MessageBus.ProviderSettings.EventProcessorHostFactory(topicGroup);

            var eventProcessorOptions = MessageBus.ProviderSettings.EventProcessorOptionsFactory(topicGroup);
            _processorHost.RegisterEventProcessorFactoryAsync(this, eventProcessorOptions).Wait();
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _processorHost.UnregisterEventProcessorAsync().Wait();

                _taskMarker.Stop().Wait();

                if (_partitionConsumers.Count > 0)
                {
                    _partitionConsumers.ForEach(ep => ep.DisposeSilently("EventProcessor", _logger));
                    _partitionConsumers.Clear();
                }
            }
        }

        #endregion

        #region Implementation of IEventProcessorFactory

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Creating {0} for {1}", nameof(IEventProcessor), new PartitionContextInfo(context));
            }

            var ep = _partitionConsumerFactory();
            _partitionConsumers.Add(ep);
            return ep;
        }

        #endregion
    }
}