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
    /// Processor for incomming response messages in the request-response patterns. 
    /// See also <see cref="IKafkaTopicPartitionProcessor"/>.
    /// </summary>
    public class KafkaResponseProcessor : IKafkaTopicPartitionProcessor
    {
        private readonly ILogger logger;

        private readonly RequestResponseSettings requestResponseSettings;
        private readonly MessageBusBase messageBus;
        private readonly ICheckpointTrigger checkpointTrigger;
        private readonly IMessageSerializer headerSerializer;
        private readonly IKafkaCommitController commitController;

        public KafkaResponseProcessor(RequestResponseSettings requestResponseSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, IMessageSerializer headerSerializer, ICheckpointTrigger checkpointTrigger)
        {
            this.messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            this.requestResponseSettings = requestResponseSettings;
            this.commitController = commitController;
            this.checkpointTrigger = checkpointTrigger;
            this.headerSerializer = headerSerializer;

            TopicPartition = topicPartition;

            logger = messageBus.LoggerFactory.CreateLogger<KafkaResponseProcessor>();
            logger.LogInformation("Creating for Group: {0}, Topic: {1}, Partition: {2}", requestResponseSettings.GetGroup(), requestResponseSettings.Path, topicPartition);
        }

        public KafkaResponseProcessor(RequestResponseSettings requestResponseSettings, TopicPartition topicPartition, IKafkaCommitController commitController, MessageBusBase messageBus, IMessageSerializer headerSerializer)
            : this(requestResponseSettings, topicPartition, commitController, messageBus, headerSerializer, new CheckpointTrigger(requestResponseSettings))
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
                var headers = message.ToHeaders(headerSerializer);
                await messageBus.OnResponseArrived(message.Message.Value, requestResponseSettings.Path, headers).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(e, "Error occured while consuming response message: {0}", new MessageContextInfo(requestResponseSettings.GetGroup(), message.TopicPartitionOffset));
                }
                // For response messages we can only continue and process all messages in the lease
                // ToDo: Add support for retry ?

                if (requestResponseSettings.OnResponseMessageFault != null)
                {
                    // Call the hook
                    logger.LogTrace("Executing the attached hook from {0}", nameof(requestResponseSettings.OnResponseMessageFault));
                    try
                    {
                        requestResponseSettings.OnResponseMessageFault(requestResponseSettings, message, e);
                    }
                    catch (Exception e2)
                    {
                        logger.LogWarning(e2, "Error handling hook failed for message: {0}", new MessageContextInfo(requestResponseSettings.GetGroup(), message.TopicPartitionOffset));
                    }
                }
            }
            if (checkpointTrigger.Increment())
            {
                commitController.Commit(message.TopicPartitionOffset);
            }
        }

        public ValueTask OnPartitionEndReached(TopicPartitionOffset offset)
        {
            commitController.Commit(offset);
            return new ValueTask();
        }

        public void OnPartitionRevoked()
        {
            checkpointTrigger.Reset();
        }

        public ValueTask OnClose()
        {
            return new ValueTask();
        }

        #endregion
    }
}