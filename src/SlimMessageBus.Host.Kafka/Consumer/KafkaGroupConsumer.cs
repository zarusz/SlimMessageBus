namespace SlimMessageBus.Host.Kafka
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Collections;
    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;
    using IConsumer = Confluent.Kafka.IConsumer<Confluent.Kafka.Ignore, byte[]>;

    public class KafkaGroupConsumer : IDisposable, IKafkaCommitController
    {
        private readonly ILogger _logger;

        public KafkaMessageBus MessageBus { get; }
        public string Group { get; }
        public ICollection<string> Topics { get; }

        private readonly SafeDictionaryWrapper<TopicPartition, IKafkaPartitionConsumer> _processors;
        private IConsumer _consumer;

        private Task _consumerTask;
        private CancellationTokenSource _consumerCts;

        public KafkaGroupConsumer(KafkaMessageBus messageBus, string group, string[] topics, Func<TopicPartition, IKafkaCommitController, IKafkaPartitionConsumer> processorFactory)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Topics = topics ?? throw new ArgumentNullException(nameof(topics));

            _logger = messageBus.LoggerFactory.CreateLogger<KafkaGroupConsumer>();

            _logger.LogInformation("Creating for Group: {Group}, Topics: {Topics}", group, string.Join(", ", topics));

            _processors = new SafeDictionaryWrapper<TopicPartition, IKafkaPartitionConsumer>(tp => processorFactory(tp, this));

            _consumer = CreateConsumer(group);
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
                if (_consumerTask != null)
                {
                    Stop();
                }

                _processors.Clear(x => x.DisposeSilently("processor", _logger));

                // dispose the consumer
                if (_consumer != null)
                {
                    _consumer.DisposeSilently("consumer", _logger);
                    _consumer = null;
                }
            }
        }

        #endregion

        protected IConsumer CreateConsumer(string group)
        {
            var config = new ConsumerConfig
            {
                GroupId = group,
                BootstrapServers = MessageBus.ProviderSettings.BrokerList
            };
            MessageBus.ProviderSettings.ConsumerConfig(config);

            // ToDo: add support for auto commit
            config.EnableAutoCommit = false;
            // Notify when we reach EoF, so that we can do a manual commit
            config.EnablePartitionEof = true;

            var consumer = MessageBus.ProviderSettings.ConsumerBuilderFactory(config)
                .SetStatisticsHandler((_, json) => OnStatistics(json))
                .SetPartitionsAssignedHandler((_, partitions) => OnPartitionAssigned(partitions))
                .SetPartitionsRevokedHandler((_, partitions) => OnPartitionRevoked(partitions))
                .SetOffsetsCommittedHandler((_, offsets) => OnOffsetsCommitted(offsets))
                .Build();

            return consumer;
        }

        public void Start()
        {
            if (_consumerTask != null)
            {
                throw new MessageBusException($"Consumer for group {Group} already started");
            }

            _consumerCts = new CancellationTokenSource();
            _consumerTask = Task.Factory.StartNew(ConsumerLoop, _consumerCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// The consumer group loop
        /// </summary>
        protected virtual async Task ConsumerLoop()
        {
            _logger.LogInformation("Group [{Group}]: Subscribing to topics: {Topics}", Group, string.Join(", ", Topics));
            _consumer.Subscribe(Topics);

            _logger.LogInformation("Group [{Group}]: Consumer loop started", Group);
            try
            {
                try
                {
                    for (var cancellationToken = _consumerCts.Token; !cancellationToken.IsCancellationRequested;)
                    {
                        try
                        {
                            _logger.LogTrace("Group [{Group}]: Polling consumer", Group);
                            var consumeResult = _consumer.Consume(cancellationToken);
                            if (consumeResult.IsPartitionEOF)
                            {
                                OnPartitionEndReached(consumeResult.TopicPartitionOffset);
                            }
                            else
                            {
                                await OnMessage(consumeResult).ConfigureAwait(false);
                            }
                        }
                        catch (ConsumeException e)
                        {
                            var pollRetryInterval = MessageBus.ProviderSettings.ConsumerPollRetryInterval;

                            _logger.LogError(e, "Group [{Group}]: Error occured while polling new messages (will retry in {RetryInterval}) - {Reason}", Group, pollRetryInterval, e.Error.Reason);
                            await Task.Delay(pollRetryInterval, _consumerCts.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }

                _logger.LogInformation("Group [{Group}]: Unsubscribing from topics", Group);
                _consumer.Unsubscribe();

                if (MessageBus.ProviderSettings.EnableCommitOnBusStop)
                {
                    OnClose();
                }

                // Ensure the consumer leaves the group cleanly and final offsets are committed.
                _consumer.Close();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Group [{Group}]: Error occured in group loop (terminated)", Group);
            }
            finally
            {
                _logger.LogInformation("Group [{Group}]: Consumer loop finished", Group);
            }
        }

        public void Stop()
        {
            if (_consumerTask == null)
            {
                throw new MessageBusException($"Consumer for group {Group} not yet started");
            }

            _consumerCts.Cancel();
            try
            {
                _consumerTask.Wait();
            }
            finally
            {
                _consumerTask = null;

                _consumerCts.DisposeSilently();
                _consumerCts = null;
            }
        }

        protected virtual void OnPartitionAssigned([NotNull] ICollection<TopicPartition> partitions)
        {
            // Ensure processors exist for each assigned topic-partition
            foreach (var partition in partitions)
            {
                _logger.LogDebug("Group [{Group}]: Assigned partition, Topic: {Topic}, Partition: {Partition}", Group, partition.Topic, partition.Partition);

                var processor = _processors.GetOrAdd(partition);
                processor.OnPartitionAssigned(partition);
            }
        }

        protected virtual void OnPartitionRevoked([NotNull] ICollection<TopicPartitionOffset> partitions)
        {
            foreach (var partition in partitions)
            {
                _logger.LogDebug("Group [{Group}]: Revoked Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", Group, partition.Topic, partition.Partition, partition.Offset);

                var processor = _processors.Dictonary[partition.TopicPartition];
                processor.OnPartitionRevoked();
            }
        }

        protected virtual void OnPartitionEndReached([NotNull] TopicPartitionOffset offset)
        {
            _logger.LogDebug("Group [{Group}]: Reached end of partition, Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", Group, offset.Topic, offset.Partition, offset.Offset);

            var processor = _processors.Dictonary[offset.TopicPartition];
            processor.OnPartitionEndReached(offset);
        }

        protected virtual async ValueTask OnMessage([NotNull] ConsumeResult message)
        {
            _logger.LogDebug("Group [{Group}]: Received message with Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, payload size: {MessageSize}", Group, message.Topic, message.Partition, message.Offset, message.Message.Value?.Length ?? 0);

            var processor = _processors.Dictonary[message.TopicPartition];
            await processor.OnMessage(message).ConfigureAwait(false);
        }

        protected virtual void OnOffsetsCommitted([NotNull] CommittedOffsets e)
        {
            if (e.Error.IsError || e.Error.IsFatal)
            {
                _logger.LogWarning("Group [{Group}]: Failed to commit offsets: [{Offsets}], error: {error}", Group, string.Join(", ", e.Offsets), e.Error.Reason);
            }
            else
            {
                _logger.LogTrace("Group [{Group}]: Successfully committed offsets: [{Offsets}]", Group, string.Join(", ", e.Offsets));
            }
        }

        protected virtual void OnClose()
        {
            var processors = _processors.Snapshot();
            foreach (var processor in processors)
            {
                processor.OnClose();
            }
        }

        protected virtual void OnStatistics(string json)
        {
            _logger.LogTrace("Group [{Group}]: Statistics: {statistics}", Group, json);
        }

        #region Implementation of IKafkaCoordinator

        public void Commit(TopicPartitionOffset offset)
        {
            _logger.LogDebug("Group [{Group}]: Commit Offset, Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", Group, offset.Topic, offset.Partition, offset.Offset);
            _consumer.Commit(new[] { offset });
        }

        #endregion
    }
}