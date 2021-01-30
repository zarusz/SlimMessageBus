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
    /// Processor for incomming response messages in the request-response patterns. 
    /// See also <see cref="IKafkaTopicPartitionProcessor"/>.
    /// </summary>
    public class KafkaResponseProcessor : IKafkaTopicPartitionProcessor
    {
        private readonly ILogger _logger;

        private readonly RequestResponseSettings _requestResponseSettings;
        private readonly MessageBusBase _messageBus;
        private readonly ICheckpointTrigger _checkpointTrigger;
        private readonly IKafkaCommitController _commitController;

        public KafkaResponseProcessor(RequestResponseSettings requestResponseSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, ICheckpointTrigger checkpointTrigger)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _requestResponseSettings = requestResponseSettings;
            TopicPartition = topicPartition;
            _commitController = commitController;
            _checkpointTrigger = checkpointTrigger;

            _logger = messageBus.LoggerFactory.CreateLogger<KafkaResponseProcessor>();
            _logger.LogInformation("Creating for Group: {0}, Topic: {1}, Partition: {2}", requestResponseSettings.GetGroup(), requestResponseSettings.Topic, topicPartition);
        }

        public KafkaResponseProcessor(RequestResponseSettings requestResponseSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus)
            : this(requestResponseSettings, topicPartition, commitController, messageBus, new CheckpointTrigger(requestResponseSettings))
        {
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        #endregion

        #region Implementation of IKafkaTopicPartitionProcessor

        public TopicPartition TopicPartition { get; }

        public async ValueTask OnMessage([NotNull] ConsumeResult message)
        {
            try
            {
                await _messageBus.OnResponseArrived(message.Message.Value, _requestResponseSettings.Topic).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(e, "Error occured while consuming response message: {0}", new MessageContextInfo(_requestResponseSettings.GetGroup(), message.TopicPartitionOffset));
                }
                // For response messages we can only continue and process all messages in the lease
                // ToDo: Add support for retry ?

                if (_requestResponseSettings.OnResponseMessageFault != null)
                {
                    // Call the hook
                    _logger.LogTrace("Executing the attached hook from {0}", nameof(_requestResponseSettings.OnResponseMessageFault));
                    try
                    {
                        _requestResponseSettings.OnResponseMessageFault(_requestResponseSettings, message, e);
                    }
                    catch (Exception e2)
                    {
                        _logger.LogWarning(e2, "Error handling hook failed for message: {0}", new MessageContextInfo(_requestResponseSettings.GetGroup(), message.TopicPartitionOffset));
                    }
                }
            }
            if (_checkpointTrigger.Increment())
            {
                _commitController.Commit(message.TopicPartitionOffset);
            }
        }

        public async ValueTask OnPartitionEndReached(TopicPartitionOffset offset)
        {
            _commitController.Commit(offset);
        }

        public void OnPartitionRevoked()
        {
            _checkpointTrigger.Reset();
        }

        #endregion
    }
}