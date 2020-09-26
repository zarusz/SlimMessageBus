using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Extensions.Logging;

namespace SlimMessageBus.Host.AzureEventHub
{
    public abstract class PartitionConsumer : IEventProcessor, IDisposable
    {
        private readonly ILogger _logger;

        protected EventHubMessageBus MessageBus { get; }
        protected TaskMarker TaskMarker { get; } = new TaskMarker();

        protected PartitionConsumer(EventHubMessageBus messageBus)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _logger = messageBus.LoggerFactory.CreateLogger<PartitionConsumer>();
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
                TaskMarker.Stop().Wait();
            }
        }

        #endregion

        #region Implementation of IEventProcessor

        public Task OpenAsync(PartitionContext context)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Open lease: {0}", new PartitionContextInfo(context));
            }
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
            _logger.LogError(error, "Partition {0} error", new PartitionContextInfo(context));
            return Task.CompletedTask;
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Close lease: Reason: {0}, {1}", reason, new PartitionContextInfo(context));
            }                                                                                                                    
            return Task.CompletedTask;
        }

        #endregion

        private async Task<EventData> CheckpointSafe(EventData message, PartitionContext context)
        {
            var lastMessageToCheckpoint = message;

            var commitResult = await OnCommit().ConfigureAwait(false);
            if (!commitResult.Success)
            {                             
                // something went wrong (not all messages were processed with success)

                // checkpoint all the succeeded messages (in order) until the first failed one
                lastMessageToCheckpoint = commitResult.LastSuccessMessage;

                // call the error hook of all the ones that failed
                // ToDo: call the hook
            }
            if (lastMessageToCheckpoint != null)
            {
                await Checkpoint(lastMessageToCheckpoint, context).ConfigureAwait(false);
            }
            return lastMessageToCheckpoint;
        }

        private Task Checkpoint(EventData message, PartitionContext context)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Will checkpoint at Offset: {0}, {1}", message.SystemProperties.Offset, new PartitionContextInfo(context));
            }
            return context.CheckpointAsync(message);
        }

        protected abstract bool OnSubmit(EventData message, PartitionContext context);
        protected abstract Task<MessageQueueResult<EventData>> OnCommit();
    }
}