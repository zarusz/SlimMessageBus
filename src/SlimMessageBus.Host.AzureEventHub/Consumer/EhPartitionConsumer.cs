namespace SlimMessageBus.Host.AzureEventHub
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Azure.Messaging.EventHubs;
    using Azure.Messaging.EventHubs.Processor;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    public abstract class EhPartitionConsumer
    {
        private readonly ILogger logger;
        protected EventHubMessageBus MessageBus { get; }
        protected ICheckpointTrigger CheckpointTrigger { get; }
        protected AbstractConsumerSettings ConsumerSettings { get; }
        protected IMessageProcessor<EventData> MessageProcessor { get; }

        public PathGroup PathGroup { get; }
        public string PartitionId { get; }

        private ProcessEventArgs lastMessage;
        private ProcessEventArgs lastCheckpointMessage;

        protected EhPartitionConsumer(EventHubMessageBus messageBus, AbstractConsumerSettings consumerSettings, IMessageProcessor<EventData> messageProcessor, PathGroup pathGroup, string partitionId)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            logger = messageBus.LoggerFactory.CreateLogger<EhPartitionConsumer>();
            PathGroup = pathGroup ?? throw new ArgumentNullException(nameof(pathGroup));
            PartitionId = partitionId ?? throw new ArgumentNullException(nameof(partitionId));

            ConsumerSettings = consumerSettings;
            MessageProcessor = messageProcessor;

            CheckpointTrigger = new CheckpointTrigger(consumerSettings, messageBus.LoggerFactory);
        }

        public Task OpenAsync()
        {
            logger.LogDebug("Open lease - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", PathGroup.Group, PathGroup.Path, PartitionId);
            CheckpointTrigger.Reset();
            return Task.CompletedTask;
        }

        public Task CloseAsync(ProcessingStoppedReason reason)
        {
            logger.LogDebug("Close lease - Reason: {Reason}, Group: {Group}, Path: {Path}, PartitionId: {PartitionId}", reason, PathGroup.Group, PathGroup.Path, PartitionId);
            return Task.CompletedTask;
        }

        public async Task ProcessEventAsync(ProcessEventArgs args)
        {
            logger.LogDebug("Message arrived - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Offset: {Offset}", PathGroup.Group, PathGroup.Path, PartitionId, args.Data.Offset);

            lastMessage = args;

            // ToDo: Pass consumerInvoker
            var lastException = await MessageProcessor.ProcessMessage(args.Data, consumerInvoker: null).ConfigureAwait(false);
            if (lastException != null)
            {
                // Note: The OnMessageFaulted was called at this point by the MessageProcessor.
                logger.LogError(lastException, "Message error - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Offset: {Offset}", PathGroup.Group, PathGroup.Path, PartitionId, args.Data.Offset);

                // ToDo: Retry logic in the future
            }

            if (CheckpointTrigger.Increment())
            {
                await Checkpoint(args).ConfigureAwait(false);
            }
        }

        public Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            // ToDo: improve error handling
            logger.LogError(args.Exception, "Partition error - Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Operation: {Operation}", PathGroup.Group, PathGroup.Path, PartitionId, args.Operation);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Forces the checkpoint to happen if (the last message hasn't yet been checkpointed already).
        /// </summary>
        /// <returns></returns>
        private async Task Checkpoint(ProcessEventArgs args)
        {
            if (args.HasEvent && (!lastCheckpointMessage.HasEvent || lastCheckpointMessage.Data.Offset < args.Data.Offset))
            {
                logger.LogDebug("Checkpoint at Group: {Group}, Path: {Path}, PartitionId: {PartitionId}, Offset: {Offset}", PathGroup.Group, PathGroup.Path, PartitionId, args.Data.Offset);
                await args.UpdateCheckpointAsync();

                CheckpointTrigger.Reset();

                lastCheckpointMessage = args;
            }
        }

        public Task TryCheckpoint()
            => Checkpoint(lastMessage);

        protected static MessageWithHeaders GetMessageWithHeaders([NotNull] EventData e) => new(e.Body.ToArray(), e.Properties);
    }
}