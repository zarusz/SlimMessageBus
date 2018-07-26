using Common.Logging;
using Confluent.Kafka;
using SlimMessageBus.Host.Config;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace SlimMessageBus.Host.Kafka
{
    /// <summary>
    /// Processor for regular consumers. 
    /// See also <see cref="IKafkaTopicPartitionProcessor"/>.
    /// </summary>
    public class KafkaConsumerProcessor : IKafkaTopicPartitionProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaConsumerProcessor>();

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
                       new ConsumerInstancePool<Message>(consumerSettings, messageBus, m => m.Value, (m, ctx) => ctx.SetTransportMessage(m)),
                       new CheckpointTrigger(consumerSettings)))
        {
        }

        public KafkaConsumerProcessor(ConsumerSettings consumerSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, MessageQueueWorker<Message> messageQueueWorker)
        {
            Log.InfoFormat(CultureInfo.InvariantCulture, "Creating for Group: {0}, Topic: {1}, Partition: {2}, MessageType: {3}", consumerSettings.Group, consumerSettings.Topic, topicPartition, consumerSettings.MessageType);

            _messageBus = messageBus;
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
                    Log.DebugFormat(CultureInfo.InvariantCulture, "Group [{0}]: Will commit at offset {1}", _consumerSettings.Group, message.TopicPartitionOffset);
                    return Commit(message.TopicPartitionOffset);
                }
            }
            catch (Exception e)
            {
                Log.ErrorFormat(CultureInfo.InvariantCulture, "Group [{0}]: Error occured while consuming a message: {0}, of type {1}", e, _consumerSettings.Group, message.TopicPartitionOffset, _consumerSettings.MessageType);
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

        public Task Commit(TopicPartitionOffset offset)
        {
            _messageQueueWorker.WaitAll(out var lastGoodMessage);
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
            return _commitController.Commit(offset);
        }
    }
}