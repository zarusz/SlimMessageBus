using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Kafka.Configs;

using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

namespace SlimMessageBus.Host.Kafka
{
    /// <summary>
    /// Processor for regular consumers. 
    /// See also <see cref="IKafkaTopicPartitionProcessor"/>.
    /// </summary>
    public class KafkaConsumerProcessor : IKafkaTopicPartitionProcessor
    {
        private readonly ILogger _logger;

        private readonly MessageBusBase _messageBus;
        private readonly ConsumerSettings _consumerSettings;
        private readonly IKafkaCommitController _commitController;
        private readonly MessageQueueWorker<ConsumeResult> _messageQueueWorker;

        public KafkaConsumerProcessor(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus)
            : this(consumerSettings,
                   topicPartition,
                   commitController,
                   messageBus,
                   new MessageQueueWorker<ConsumeResult>(
                       new ConsumerInstancePoolMessageProcessor<ConsumeResult>(consumerSettings, messageBus, m => m.Message.Value, (m, ctx) => ctx.SetTransportMessage(m)),
                       new CheckpointTrigger(consumerSettings), messageBus.LoggerFactory))
        {
        }

        public KafkaConsumerProcessor(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, MessageQueueWorker<ConsumeResult> messageQueueWorker)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            _logger = _messageBus.LoggerFactory.CreateLogger<KafkaConsumerProcessor>();
            _logger.LogInformation("Creating for Group: {0}, Topic: {1}, Partition: {2}, MessageType: {3}", consumerSettings.GetGroup(), consumerSettings.Topic, topicPartition, consumerSettings.MessageType);

            _consumerSettings = consumerSettings;
            TopicPartition = topicPartition;
            _commitController = commitController;
            _messageQueueWorker = messageQueueWorker;
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
                _messageQueueWorker.ConsumerInstancePool.Dispose();
            }
        }

        #endregion

        #region Implementation of IKafkaTopicPartitionProcessor

        public TopicPartition TopicPartition { get; }

        public async ValueTask OnMessage([NotNull] ConsumeResult message)
        {
            try
            {
                if (_messageQueueWorker.Submit(message))
                {
                    _logger.LogDebug("Group [{group}]: Will commit at offset {offset}", _consumerSettings.GetGroup(), message.TopicPartitionOffset);
                    await Commit(message.TopicPartitionOffset).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Group [{group}]: Error occured while consuming a message at offset: {offset}, of type {messageType}", _consumerSettings.GetGroup(), message.TopicPartitionOffset, _consumerSettings.MessageType);
                throw;
            }
        }

        public async ValueTask OnPartitionEndReached(TopicPartitionOffset offset)
        {
            await Commit(offset).ConfigureAwait(false);
        }

        public async ValueTask OnPartitionRevoked()
        {
            _messageQueueWorker.Clear();
        }

        #endregion

        public async ValueTask Commit(TopicPartitionOffset offset)
        {
            var result = await _messageQueueWorker.WaitAll().ConfigureAwait(false);
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
            await _commitController.Commit(offset).ConfigureAwait(false);
        }
    }
}