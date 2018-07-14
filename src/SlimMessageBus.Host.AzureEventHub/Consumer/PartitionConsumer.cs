using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;

namespace SlimMessageBus.Host.AzureEventHub
{
    public abstract class PartitionConsumer : IEventProcessor, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected EventHubMessageBus MessageBus { get; }
        protected TaskMarker TaskMarker { get; } = new TaskMarker();

        protected PartitionConsumer(EventHubMessageBus messageBus)
        {
            MessageBus = messageBus;
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected  virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                TaskMarker.Stop().Wait();
            }
        }

        #endregion

        #region Implementation of IEventProcessor

        public Task OpenAsync(PartitionContext context)
        {
            Log.DebugFormat(CultureInfo.InvariantCulture, "Open lease: {0}", new PartitionContextInfo(context));
            return Task.CompletedTask;
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            TaskMarker.OnStarted();
            try
            {
                EventData lastMessage = null;
                EventData lastCheckpointMessage = null;
                var skipLastCheckpoint = false;

                foreach (var message in messages)
                {
                    if (!TaskMarker.CanRun)
                    {
                        break;
                    }

                    lastMessage = message;
                    if (OnSubmit(message, context))
                    {
                        lastCheckpointMessage = await CheckpointSafe(message, context).ConfigureAwait(false);
                        if (!ReferenceEquals(lastCheckpointMessage, message))
                        {
                            // something went wrong (not all messages were processed with success)

                            // ToDo: add retry support
                            //skipLastCheckpoint = !ReferenceEquals(lastCheckpointMessage, message);
                            //skipLastCheckpoint = false;
                        }
                    }
                }

                if (!skipLastCheckpoint)
                {
                    // checkpoint the last messages
                    if ((!ReferenceEquals(lastCheckpointMessage, lastMessage) && lastMessage != null))
                    {
                        await CheckpointSafe(lastMessage, context).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                TaskMarker.OnFinished();
            }
        }

        public Task ProcessErrorAsync(PartitionContext context, Exception error)
        {
            // ToDo: improve error handling
            Log.ErrorFormat(CultureInfo.InvariantCulture, "Partition {0} error", error, new PartitionContextInfo(context));
            return Task.CompletedTask;
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            Log.DebugFormat(CultureInfo.InvariantCulture, "Close lease: Reason: {0}, {1}", reason, new PartitionContextInfo(context));
            return Task.CompletedTask;
        }

        #endregion

        private async Task<EventData> CheckpointSafe(EventData message, PartitionContext context)
        {
            EventData lastCheckpointMessage;

            if (OnCommit(out var lastGoodMessage))
            {
                // all messages were successful
                lastCheckpointMessage = message;
            }
            else
            {
                // something went wrong (not all messages were processed with success)

                // checkpoint all the succeeded messages (in order) until the first failed one
                lastCheckpointMessage = lastGoodMessage;

                // call the hook of all the ones that failed
                // ToDo: call the hook
            }
            if (lastCheckpointMessage != null)
            {
                await Checkpoint(lastCheckpointMessage, context).ConfigureAwait(false);
            }
            return lastCheckpointMessage;
        }

        private static Task Checkpoint(EventData message, PartitionContext context)
        {
            Log.DebugFormat(CultureInfo.InvariantCulture, "Will checkpoint at Offset: {0}, {1}", message.SystemProperties.Offset, new PartitionContextInfo(context));
            return context.CheckpointAsync(message);
        }

        protected abstract bool OnSubmit(EventData message, PartitionContext context);
        protected abstract bool OnCommit(out EventData lastGoodMessage);
    }
}