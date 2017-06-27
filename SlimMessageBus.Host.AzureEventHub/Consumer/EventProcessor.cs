using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;

namespace SlimMessageBus.Host.AzureEventHub
{
    public abstract class EventProcessor : IEventProcessor, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<EventProcessor>();

        protected readonly EventHubConsumer Consumer;
        private int _lastCheckpointCount;
        private readonly Stopwatch _lastCheckpointDuration = new Stopwatch();

        protected int CheckpointCount = Consts.CheckpointCountDefault;
        protected int CheckpointDuration = Consts.CheckpointDurationDefault;

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

            _lastCheckpointCount = 0;
            _lastCheckpointDuration.Start();

            return Task.CompletedTask;
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

                await OnSubmit(message);
                lastMessage = message;
                _lastCheckpointCount++;

                if (_lastCheckpointCount >= CheckpointCount || _lastCheckpointDuration.ElapsedMilliseconds > CheckpointDuration)
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

            _lastCheckpointCount = 0;
            _lastCheckpointDuration.Restart();            

            return messageToCheckpoint;
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Log.DebugFormat("Close lease: Reason: {0}, {1}", reason, new PartitionContextInfo(context));

            _lastCheckpointCount = 0;
            _lastCheckpointDuration.Stop();

            return Task.CompletedTask;
        }

        #endregion

        protected abstract Task OnSubmit(EventData message);
        protected abstract Task<EventData> OnCommit(EventData lastMessage);
    }
}