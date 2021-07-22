namespace SlimMessageBus.Host.Kafka
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.Serialization;
    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

    /// <summary>
    /// Processor for regular consumers. 
    /// See also <see cref="IKafkaTopicPartitionProcessor"/>.
    /// </summary>
    public class KafkaConsumerProcessor : IKafkaTopicPartitionProcessor
    {
        private readonly ILogger _logger;

        private readonly MessageBusBase messageBus;
        private readonly ConsumerSettings consumerSettings;
        private readonly IKafkaCommitController commitController;
        private readonly MessageQueueWorker<ConsumeResult> messageQueueWorker;

        public KafkaConsumerProcessor(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, [NotNull] MessageBusBase messageBus, [NotNull] IMessageSerializer headerSerializer)
            : this(consumerSettings,
                   topicPartition,
                   commitController,
                   messageBus,
                   new MessageQueueWorker<ConsumeResult>(
                       new ConsumerInstancePoolMessageProcessor<ConsumeResult>(
                           consumerSettings,
                           messageBus,
                           m => m.ToMessageWithHeaders(headerSerializer),
                           (m, ctx) => ctx.SetTransportMessage(m)),
                       new CheckpointTrigger(consumerSettings), messageBus.LoggerFactory))
        {
        }

        public KafkaConsumerProcessor(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, MessageQueueWorker<ConsumeResult> messageQueueWorker)
        {
            this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            _logger = this.messageBus.LoggerFactory.CreateLogger<KafkaConsumerProcessor>();
            _logger.LogInformation("Creating for Group: {0}, Topic: {1}, Partition: {2}, MessageType: {3}", consumerSettings.GetGroup(), consumerSettings.Path, topicPartition, consumerSettings.MessageType);

            this.consumerSettings = consumerSettings;
            this.commitController = commitController;
            this.messageQueueWorker = messageQueueWorker;
            TopicPartition = topicPartition;
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
                messageQueueWorker.Dispose();
            }
        }

        #endregion

        #region Implementation of IKafkaTopicPartitionProcessor

        public TopicPartition TopicPartition { get; }

        public async ValueTask OnMessage([NotNull] ConsumeResult message)
        {
            try
            {
                if (messageQueueWorker.Submit(message))
                {
                    _logger.LogDebug("Group [{group}]: Will commit at offset {offset}", consumerSettings.GetGroup(), message.TopicPartitionOffset);
                    await Commit().ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Group [{group}]: Error occured while consuming a message at offset: {offset}, of type {messageType}", consumerSettings.GetGroup(), message.TopicPartitionOffset, consumerSettings.MessageType);
                throw;
            }
        }

        public async ValueTask OnPartitionEndReached(TopicPartitionOffset offset)
        {
            await Commit().ConfigureAwait(false);
        }

        public void OnPartitionRevoked()
        {
            messageQueueWorker.Clear();
        }

        public async ValueTask OnClose()
        {
            await Commit().ConfigureAwait(false);
        }

        #endregion

        public async ValueTask Commit()
        {
            var result = await messageQueueWorker.WaitAll().ConfigureAwait(false);
            // ToDo: Add retry functionality
            /*
            if (lastGoodMessage == null || lastGoodMessage.TopicPartitionOffset != offset)
            {
                if (lastGoodMessage != null)
                {
                    await _commitController.Commit(lastGoodMessage.TopicPartitionOffset);
                }

                // ToDo: Add retry functionality
            }
            */
            if (result.LastSuccessMessage != null)
            {
                var offsetToCommit = result.LastSuccessMessage.TopicPartitionOffset;
                commitController.Commit(offsetToCommit.AddOffset(1));
            }
        }
    }
}