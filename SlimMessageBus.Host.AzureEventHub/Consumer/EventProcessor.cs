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
        private int _numMessagesProcessed;

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
            _numMessagesProcessed = 0;
            return Task.CompletedTask;
        }

        // ToDo: Extract this into config param
        private int _commitBatchSize = 10;

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

                await OnSubmit(message);
                lastMessage = message;
                _numMessagesProcessed++;

                // ToDo: add timer trigger
                if (_numMessagesProcessed >= _commitBatchSize)
                {
                    lastCheckpointMessage = await Checkpoint(message, context);
                    // something went wrong (not all messages were processed with success)
                    skipLastCheckpoint = lastCheckpointMessage != message;
                }
            }

            if (!skipLastCheckpoint && lastCheckpointMessage != lastMessage && lastMessage != null)
            {
                // checkpoint the last message (if all was succesful until now)
                await Checkpoint(lastMessage, context);
            }
        }

        private async Task<EventData> Checkpoint(EventData message, PartitionContext context)
        {
            var messageToCheckpoint = await OnCommit(message);

            Log.DebugFormat("Will checkpoint at Offset: {0}, {1}", message.Offset, new PartitionContextInfo(context));
            await context.CheckpointAsync(messageToCheckpoint);

            _numMessagesProcessed = 0;

            return messageToCheckpoint;
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Log.DebugFormat("Close lease: Reason: {0}, {1}", reason, new PartitionContextInfo(context));
            return Task.CompletedTask;
        }

        #endregion

        protected abstract Task OnSubmit(EventData message);
        protected abstract Task<EventData> OnCommit(EventData lastMessage);
    }
}