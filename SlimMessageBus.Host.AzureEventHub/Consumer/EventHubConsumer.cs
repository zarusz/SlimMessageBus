using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class EventHubConsumer : IDisposable, IEventProcessorFactory
    {
        private static readonly ILog Log = LogManager.GetLogger<EventHubConsumer>();

        public readonly EventHubMessageBus MessageBus;
        public readonly string Group;
        public readonly string Topic;
        public readonly Type MessageType;

        internal protected TopicConsumerInstances<EventData> Instances;
        protected EventProcessorHost EventProcessorHost;

        internal protected bool CanRun = true;

        public EventHubConsumer(EventHubMessageBus messageBus, ConsumerSettings consumerSettings)
        {
            MessageBus = messageBus;
            Group = consumerSettings.Group;
            Topic = consumerSettings.Topic;
            MessageType = consumerSettings.MessageType;

            Instances = new TopicConsumerInstances<EventData>(consumerSettings, this, MessageBus, e => e.GetBytes());

            Log.InfoFormat("Creating EventProcessorHost for topic {0}, group: {1}", Topic, Group);

            EventProcessorHost = MessageBus.EventHubSettings.EventProcessorHostFactory(consumerSettings);
            var eventProcessorOptions = MessageBus.EventHubSettings.EventProcessorOptionsFactory(consumerSettings);
            EventProcessorHost.RegisterEventProcessorFactoryAsync(this, eventProcessorOptions).Wait();
        }

        public EventHubConsumer(EventHubMessageBus messageBus, RequestResponseSettings requestResponse)
        {
            MessageBus = messageBus;
            Group = requestResponse.Group;
            Topic = requestResponse.Topic;
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

            if (Instances != null)
            {
                Instances.DisposeSilently("consumer instances", Log);
                Instances = null;
            }
        }

        #endregion

        #region Implementation of IEventProcessorFactory

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            Log.DebugFormat("Creating EventHubEventProcessor for EventHubPath: {0}, ConsumerGroupName: {1}", context.EventHubPath, context.ConsumerGroupName);
            var ep = new EventProcessor(this);
            return ep;
        }

        #endregion
    }

    public class EventProcessor : IEventProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger<EventProcessor>();

        private readonly EventHubConsumer _consumer;

        public EventProcessor(EventHubConsumer consumer)
        {
            _consumer = consumer;
        }

        #region Implementation of IEventProcessor

        public Task OpenAsync(PartitionContext context)
        {
            Log.DebugFormat("Open lease {0}", context.Lease);
            return Task.FromResult<object>(null);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            EventData lastMessage = null;
            EventData lastCheckpointMessage = null;
            var skipLastCheckpoint = false;

            foreach (var message in messages)
            {
                if (!_consumer.CanRun)
                {
                    break;
                }

                var messageToCheckpoint = _consumer.Instances.Submit(message);
                if (messageToCheckpoint != null)
                {
                    Log.DebugFormat("Will checkpoint at offset {0}, sequence {1}, topic {2}, group {3}", message.Offset, message.SequenceNumber, _consumer.Topic, _consumer.Group);
                    await context.CheckpointAsync(messageToCheckpoint);

                    skipLastCheckpoint = messageToCheckpoint != message;

                    lastCheckpointMessage = messageToCheckpoint;
                }
                lastMessage = message;
            }

            if (!skipLastCheckpoint && lastCheckpointMessage != lastMessage && lastMessage != null)
            {
                var messageToCommit = _consumer.Instances.Commit(lastMessage);
                await context.CheckpointAsync(messageToCommit);
            }
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Log.DebugFormat("Close lease {0}, reason {1}", context.Lease, reason);
            return Task.FromResult<object>(null);
        }

        #endregion
    }


}