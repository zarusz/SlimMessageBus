namespace SlimMessageBus.Host.Kafka
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

    public abstract class KafkaPartitionConsumer : IKafkaPartitionConsumer
    {
        private readonly ILogger logger;

        private readonly MessageBusBase messageBus;
        private readonly AbstractConsumerSettings consumerSettings;
        private readonly IKafkaCommitController commitController;
        private readonly IMessageProcessor<ConsumeResult> messageProcessor;

        public ICheckpointTrigger CheckpointTrigger { get; set; }

        private TopicPartitionOffset lastOffset;
        private TopicPartitionOffset lastCheckpointOffset;

        protected KafkaPartitionConsumer(AbstractConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, IMessageProcessor<ConsumeResult> messageProcessor)
        {
            this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            logger = this.messageBus.LoggerFactory.CreateLogger<KafkaPartitionConsumerForConsumers>();
            logger.LogInformation("Creating consumer for Group: {Group}, Topic: {Topic}, Partition: {Partition}", consumerSettings.GetGroup(), consumerSettings.Path, topicPartition.Partition);

            TopicPartition = topicPartition;

            this.consumerSettings = consumerSettings;
            this.commitController = commitController;
            this.messageProcessor = messageProcessor;

            // ToDo: Add support for Kafka driven automatic commit
            this.CheckpointTrigger = new CheckpointTrigger(consumerSettings);
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                messageProcessor.Dispose();
            }
        }

        #endregion

        #region Implementation of IKafkaTopicPartitionProcessor

        public TopicPartition TopicPartition { get; }

        public void OnPartitionAssigned([NotNull] TopicPartition partition)
        {
            lastCheckpointOffset = null;
            lastOffset = null;

            CheckpointTrigger?.Reset();
        }

        public async Task OnMessage([NotNull] ConsumeResult message)
        {
            try
            {
                lastOffset = message.TopicPartitionOffset;

                // ToDo: Pass consumerInvoker
                var lastException = await messageProcessor.ProcessMessage(message, consumerInvoker: null).ConfigureAwait(false);
                if (lastException != null)
                {
                    // ToDo: Retry logic
                    // The OnMessageFaulted was called at this point by the MessageProcessor.
                }

                if (CheckpointTrigger != null && CheckpointTrigger.Increment())
                {
                    Commit(message.TopicPartitionOffset);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Group [{Group}]: Error occured while consuming a message at Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", consumerSettings.GetGroup(), message.Topic, message.Partition, message.Offset);
                throw;
            }
        }

        public void OnPartitionEndReached(TopicPartitionOffset offset)
        {
            if (CheckpointTrigger != null)
            {
                if (offset != null)
                {
                    Commit(offset);
                }
            }
        }

        public void OnPartitionRevoked()
        {
            if (CheckpointTrigger != null)
            {
            }
        }

        public void OnClose()
        {
            if (CheckpointTrigger != null)
            {
                Commit(lastOffset);
            }
        }

        #endregion

        public void Commit(TopicPartitionOffset offset)
        {
            if (lastCheckpointOffset == null || offset.Offset > lastCheckpointOffset.Offset)
            {
                logger.LogDebug("Group [{Group}]: Commit at Offset: {Offset}, Partition: {Partition}, Topic: {Topic}", consumerSettings.GetGroup(), offset.Offset, offset.Partition, offset.Topic);

                lastCheckpointOffset = offset;
                commitController.Commit(offset);

                CheckpointTrigger?.Reset();
            }
        }
    }
}