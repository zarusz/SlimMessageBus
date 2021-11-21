namespace SlimMessageBus.Host.AzureEventHub
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    public abstract class EhPartitionConsumer : IEventProcessor, IDisposable
    {
        private readonly ILogger logger;
        protected EventHubMessageBus MessageBus { get; }
        protected TaskMarker TaskMarker { get; } = new TaskMarker();
        protected ICheckpointTrigger CheckpointTrigger { get; }
        protected AbstractConsumerSettings ConsumerSettings { get; }
        protected IMessageProcessor<EventData> MessageProcessor { get; }

        public string PartitionId { get; }

        private EventData lastMessage;
        private EventData lastCheckpointMessage;
        private PartitionContext leaseContext;

        protected EhPartitionConsumer(EventHubMessageBus messageBus, AbstractConsumerSettings consumerSettings, IMessageProcessor<EventData> messageProcessor, string partitionId)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            logger = messageBus.LoggerFactory.CreateLogger<EhPartitionConsumer>();
            PartitionId = partitionId ?? throw new ArgumentNullException(nameof(partitionId));

            ConsumerSettings = consumerSettings;
            MessageProcessor = messageProcessor;

            // ToDo: Make the checkpoint optional - let EH control when to commit
            CheckpointTrigger = new CheckpointTrigger(consumerSettings);
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
                TaskMarker.StopAndWait().Wait();
            }
        }

        #endregion

        #region Implementation of IEventProcessor

        public Task OpenAsync([NotNull] PartitionContext context)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Open lease - PartitionId: {PartitionId}, Path: {Path}, Group: {Group}", context.PartitionId, context.EventHubPath, context.ConsumerGroupName);
            }

            leaseContext = context;

            return Task.CompletedTask;
        }

        public async Task ProcessEventsAsync([NotNull] PartitionContext context, [NotNull] IEnumerable<EventData> messages)
        {
            TaskMarker.OnStarted();
            try
            {
                foreach (var message in messages)
                {
                    if (!TaskMarker.CanRun)
                    {
                        break;
                    }

                    lastMessage = message;

                    // ToDo: Pass consumerInvoker
                    var lastException = await MessageProcessor.ProcessMessage(message, consumerInvoker: null).ConfigureAwait(false);
                    if (lastException != null)
                    {
                        // ToDo: Retry logic
                        // The OnMessageFaulted was called at this point by the MessageProcessor.
                    }
                    if (CheckpointTrigger != null && CheckpointTrigger.Increment())
                    {
                        await Checkpoint(context, message).ConfigureAwait(false);

                        lastCheckpointMessage = message;

                        if (!ReferenceEquals(lastCheckpointMessage, message))
                        {
                            // something went wrong (not all messages were processed with success)

                            // ToDo: add retry support
                            //skipLastCheckpoint = !ReferenceEquals(lastCheckpointMessage, message);
                            //skipLastCheckpoint = false;
                        }
                    }
                }
            }
            finally
            {
                TaskMarker.OnFinished();
            }
        }

        public void Stop() => TaskMarker.Stop();

        public Task ProcessErrorAsync([NotNull] PartitionContext context, Exception error)
        {
            // ToDo: improve error handling
            logger.LogError(error, "Partition error - PartitionId: {PartitionId}, Path: {Path}, Group: {Group}", context.PartitionId, context.EventHubPath, context.ConsumerGroupName);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Forces the checkpoint to happen if (the last message hasn't yet been checkpointed already).
        /// </summary>
        /// <returns></returns>

        public Task Checkpoint(PartitionContext context = null)
        {
            var effectiveContext = context ?? leaseContext;
            if (effectiveContext != null)
            {
                // checkpoint the last messages
                if (lastMessage != null && !ReferenceEquals(lastCheckpointMessage, lastMessage))
                {
                    return Checkpoint(effectiveContext, lastMessage);
                }
            }

            return Task.CompletedTask;
        }

        public Task CloseAsync([NotNull] PartitionContext context, CloseReason reason)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Close lease - Reason: {Reason}, PartitionId: {PartitionId}, Path: {Path}, Group: {Group}", reason, context.PartitionId, context.EventHubPath, context.ConsumerGroupName);
            }

            // Note: We cannot checkpoint when the lease is lost or shutdown.
            //if (CheckpointTrigger != null)
            //{
            //    return Checkpoint(context);
            //}

            return Task.CompletedTask;
        }

        #endregion        

        private async Task Checkpoint(PartitionContext context, EventData message)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Checkpoint at Offset: {Offset}, PartitionId: {PartitionId}, Path: {Path}, Group: {Group}", message.SystemProperties.Offset, context.PartitionId, context.EventHubPath, context.ConsumerGroupName);
            }
            if (message != null)
            {
                await context.CheckpointAsync(message);
                CheckpointTrigger?.Reset();
            }
        }

        protected static MessageWithHeaders GetMessageWithHeaders([NotNull] EventData e) => new MessageWithHeaders(e.Body.Array, e.Properties);
    }
}