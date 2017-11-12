using System;
using System.Threading.Tasks;
using Common.Logging;
using Confluent.Kafka;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka
{
    /// <summary>
    /// Processor for regular consumers. 
    /// See also <see cref="IKafkaTopicPartitionProcessor"/>.
    /// </summary>
    public class KafkaConsumerProcessor : IKafkaTopicPartitionProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaConsumerProcessor>();

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
            Log.InfoFormat("Creating for Group: {0}, Topic: {1}, Partition: {2}, MessageType: {3}", consumerSettings.Group, consumerSettings.Topic, topicPartition, consumerSettings.MessageType);

            _consumerSettings = consumerSettings;
            TopicPartition = topicPartition;
            _commitController = commitController;
            _messageQueueWorker = messageQueueWorker;
        }

        #region IDisposable

        public void Dispose()
        {
            _messageQueueWorker.ConsumerInstancePool.Dispose();
        }

        #endregion

        #region Implementation of IKafkaTopicPartitionProcessor

        public TopicPartition TopicPartition { get; }

        public async Task OnMessage(Message message)
        {
            try
            {
                if (_messageQueueWorker.Submit(message))
                {
                    Log.DebugFormat("Group [{0}]: Will commit at offset {1}", _consumerSettings.Group, message.TopicPartitionOffset);
                    await Commit(message.TopicPartitionOffset);
                }
            }
            catch (Exception e)
            {
                Log.ErrorFormat("Group [{0}]: Error occured while consuming a message: {0}, of type {1}", e, _consumerSettings.Group, message.TopicPartitionOffset, _consumerSettings.MessageType);
                throw;
            }
        }

        public async Task OnPartitionEndReached(TopicPartitionOffset offset)
        {
            await Commit(offset);
        }

        public Task OnPartitionRevoked()
        {
            _messageQueueWorker.Clear();
            return Task.FromResult(0);
        }

        #endregion

        public async Task Commit(TopicPartitionOffset offset)
        {
            _messageQueueWorker.WaitAll(out Message lastGoodMessage);
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
            await _commitController.Commit(offset);
        }
    }
}