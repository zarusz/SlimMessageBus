namespace SlimMessageBus.Host.AzureEventHub
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs.Processor;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Collections;
    using SlimMessageBus.Host.Config;

    public class EhGroupConsumer : IDisposable, IEventProcessorFactory
    {
        private readonly ILogger logger;

        public EventHubMessageBus MessageBus { get; }

        private readonly EventProcessorHost processorHost;
        private readonly SafeDictionaryWrapper<string, EhPartitionConsumer> partitionConsumerByPartitionId;

        public EhGroupConsumer(EventHubMessageBus messageBus, [NotNull] ConsumerSettings consumerSettings)
            : this(messageBus, new TopicGroup(consumerSettings.Path, consumerSettings.GetGroup()), (partitionId) => new EhPartitionConsumerForConsumers(messageBus, consumerSettings, partitionId))
        {
        }

        public EhGroupConsumer(EventHubMessageBus messageBus, [NotNull] RequestResponseSettings requestResponseSettings)
            : this(messageBus, new TopicGroup(requestResponseSettings.Path, requestResponseSettings.GetGroup()), (partitionId) => new EhPartitionConsumerForResponses(messageBus, requestResponseSettings, partitionId))
        {
        }

        protected EhGroupConsumer(EventHubMessageBus messageBus, TopicGroup topicGroup, Func<string, EhPartitionConsumer> partitionConsumerFactory)
        {
            _ = topicGroup ?? throw new ArgumentNullException(nameof(topicGroup));
            _ = partitionConsumerFactory ?? throw new ArgumentNullException(nameof(partitionConsumerFactory));

            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            logger = messageBus.LoggerFactory.CreateLogger<EhGroupConsumer>();

            partitionConsumerByPartitionId = new SafeDictionaryWrapper<string, EhPartitionConsumer>(partitionConsumerFactory);

            logger.LogInformation("Creating EventProcessorHost for EventHub with Path: {Path}, Group: {Group}", topicGroup.Topic, topicGroup.Group);
            processorHost = MessageBus.ProviderSettings.EventProcessorHostFactory(topicGroup);

            var eventProcessorOptions = MessageBus.ProviderSettings.EventProcessorOptionsFactory(topicGroup);
            processorHost.RegisterEventProcessorFactoryAsync(this, eventProcessorOptions).Wait();
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task Stop()
        {
            var partitionConsumers = partitionConsumerByPartitionId.Snapshot();
            foreach(var pc in partitionConsumers)
            {
                // stop all partition consumer to not access any more message
                pc.Stop();
            }

            if (MessageBus.ProviderSettings.EnableCheckpointOnBusStop)
            {
                // checkpoint anything we've processed thus far
                await Task.WhenAll(partitionConsumers.Select(pc => pc.Checkpoint()));
            }

            // stop the processing host
            await processorHost.UnregisterEventProcessorAsync();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop().Wait();

                var partitionConsumers = partitionConsumerByPartitionId.Snapshot();
                foreach (var pc in partitionConsumers)
                {
                    pc.DisposeSilently();
                }
            }
        }

        #endregion

        #region Implementation of IEventProcessorFactory

        public IEventProcessor CreateEventProcessor([NotNull] PartitionContext context)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Creating IEventProcessor for Path: {Path}, PartitionId: {PartitionId}, Offset: {Offset}", context.EventHubPath, context.PartitionId, context.Lease.Offset);
            }

            var ep = partitionConsumerByPartitionId.GetOrAdd(context.PartitionId);
            return ep;
        }

        #endregion
    }
}