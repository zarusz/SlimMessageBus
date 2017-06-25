using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;

namespace SlimMessageBus.Host.AzureEventHub
{
    public abstract class EventProcessor : IEventProcessor, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<EventProcessor>();

        protected readonly EventHubConsumer Consumer;

        protected EventProcessor(EventHubConsumer consumer)
        {
            Consumer = consumer;
        }

        #region Implementation of IDisposable

        public abstract void Dispose();

        #endregion

        #region Implementation of IEventProcessor

        public Task OpenAsync(PartitionContext context)
        {
            Log.DebugFormat("Open lease: {0}", new PartitionContextInfo(context));
            return Task.FromResult<object>(null);
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            EventData lastMessage = null;
            EventData lastCheckpointMessage = null;
            var skipLastCheckpoint = false;

            foreach (var message in messages)
            {
                if (!Consumer.CanRun)
                {
                    break;
                }

                var messageToCheckpoint = await OnSubmit(message);
                if (messageToCheckpoint != null)
                {
                    Log.DebugFormat("Will checkpoint at Offset: {0}, {1}", message.Offset, new PartitionContextInfo(context));
                    await context.CheckpointAsync(messageToCheckpoint);

                    skipLastCheckpoint = messageToCheckpoint != message;

                    lastCheckpointMessage = messageToCheckpoint;
                }
                lastMessage = message;
            }

            if (!skipLastCheckpoint && lastCheckpointMessage != lastMessage && lastMessage != null)
            {
                var messageToCommit = await OnCommit(lastMessage);
                await context.CheckpointAsync(messageToCommit);
            }
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Log.DebugFormat("Close lease: Reason: {0}, {1}", reason, new PartitionContextInfo(context));
            return Task.FromResult<object>(null);
        }

        #endregion

        protected abstract Task<EventData> OnSubmit(EventData message);
        protected abstract Task<EventData> OnCommit(EventData lastMessage);
    }
}