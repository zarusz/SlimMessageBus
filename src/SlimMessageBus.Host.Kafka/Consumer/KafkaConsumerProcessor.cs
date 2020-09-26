using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Kafka.Configs;

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
        private readonly MessageQueueWorker<Message> _messageQueueWorker;

        public KafkaConsumerProcessor(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus)
            : this(consumerSettings,
                   topicPartition,
                   commitController,
                   messageBus,
                   new MessageQueueWorker<Message>(
                       new ConsumerInstancePoolMessageProcessor<Message>(consumerSettings, messageBus, m => m.Value, (m, ctx) => ctx.SetTransportMessage(m)),
                       new CheckpointTrigger(consumerSettings), messageBus.LoggerFactory))
        {
        }

        public KafkaConsumerProcessor(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, MessageQueueWorker<Message> messageQueueWorker)
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

        public Task OnMessage(Message message)
        {
            try
            {
                if (_messageQueueWorker.Submit(message))
                {
                    _logger.LogDebug("Group [{0}]: Will commit at offset {1}", _consumerSettings.GetGroup(), message.TopicPartitionOffset);
                    return Commit(message.TopicPartitionOffset);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Group [{0}]: Error occured while consuming a message: {0}, of type {1}", _consumerSettings.GetGroup(), message.TopicPartitionOffset, _consumerSettings.MessageType);
                throw;
            }
            return Task.CompletedTask;
        }

        public Task OnPartitionEndReached(TopicPartitionOffset offset)
        {
            return Commit(offset);
        }

        public Task OnPartitionRevoked()
        {
            _messageQueueWorker.Clear();
            return Task.CompletedTask;
        }

        #endregion

        public async Task Commit(TopicPartitionOffset offset)
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