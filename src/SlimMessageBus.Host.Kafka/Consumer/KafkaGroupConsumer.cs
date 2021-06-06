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

        private readonly SafeDictionaryWrapper<TopicPartition, IKafkaTopicPartitionProcessor> _processors;
        private IConsumer _consumer;

        private Task _consumerTask;
        private CancellationTokenSource _consumerCts;

        public KafkaGroupConsumer(KafkaMessageBus messageBus, string group, string[] topics, Func<TopicPartition, IKafkaCommitController, IKafkaTopicPartitionProcessor> processorFactory)
        {
            MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Topics = topics ?? throw new ArgumentNullException(nameof(topics));

            _logger = messageBus.LoggerFactory.CreateLogger<KafkaGroupConsumer>();

            _logger.LogInformation("Creating for group: {0}, topics: {1}", group, string.Join(", ", topics));

            _processors = new SafeDictionaryWrapper<TopicPartition, IKafkaTopicPartitionProcessor>(tp => processorFactory(tp, this));

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
            _logger.LogInformation("Group [{group}]: Subscribing to topics: {topics}", Group, string.Join(", ", Topics));
            _consumer.Subscribe(Topics);

            _logger.LogInformation("Group [{group}]: Consumer loop started", Group);
            try
            {
                try
                {
                    for (var cancellationToken = _consumerCts.Token; !cancellationToken.IsCancellationRequested;)
                    {
                        try
                        {
                            _logger.LogTrace("Group [{group}]: Polling consumer", Group);
                            var consumeResult = _consumer.Consume(cancellationToken);
                            if (consumeResult.IsPartitionEOF)
                            {
                                await OnPartitionEndReached(consumeResult.TopicPartitionOffset).ConfigureAwait(false);
                            }
                            else
                            {
                                await OnMessage(consumeResult).ConfigureAwait(false);
                            }
                        }
                        catch (ConsumeException e)
                        {
                            var pollRetryInterval = MessageBus.ProviderSettings.ConsumerPollRetryInterval;

                            _logger.LogError(e, "Group [{group}]: Error occured while polling new messages (will retry in {retryInterval}) - {reason}", Group, pollRetryInterval, e.Error.Reason);
                            await Task.Delay(pollRetryInterval, _consumerCts.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException e)
                {
                }

                _logger.LogInformation("Group [{group}]: Unsubscribing from topics", Group);
                _consumer.Unsubscribe();

                await OnClose().ConfigureAwait(false);

                // Ensure the consumer leaves the group cleanly and final offsets are committed.
                _consumer.Close();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Group [{group}]: Error occured in group loop (terminated)", Group);
            }
            finally
            {
                _logger.LogInformation("Group [{group}]: Consumer loop finished", Group);
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
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Group [{group}]: Assigned partitions: {partitions}", Group, string.Join(", ", partitions));
            }

            // Ensure processors exist for each assigned topic-partition
            foreach (var partition in partitions)
            {
                _processors.GetOrAdd(partition);
            }

            //_consumer?.Assign(partitions);
        }

        protected virtual void OnPartitionRevoked([NotNull] ICollection<TopicPartitionOffset> partitions)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Group [{group}]: Revoked partitions: {partitions}", Group, string.Join(", ", partitions));
            }

            foreach (var partition in partitions)
            {
                _processors.Dictonary[partition.TopicPartition].OnPartitionRevoked();
            }

            //_consumer?.Unassign();
        }

        protected virtual async ValueTask OnPartitionEndReached([NotNull] TopicPartitionOffset offset)
        {
            _logger.LogDebug("Group [{group}]: Reached end of partition: {partition}, next message will be at offset: {offset}", Group, offset.TopicPartition, offset.Offset);

            var processor = _processors.Dictonary[offset.TopicPartition];
            await processor.OnPartitionEndReached(offset).ConfigureAwait(false);
        }

        protected virtual async ValueTask OnMessage([NotNull] ConsumeResult message)
        {
            _logger.LogDebug("Group [{group}]: Received message with offset: {offset}, payload size: {messageSize}", Group, message.TopicPartitionOffset, message.Message.Value?.Length ?? 0);

            var processor = _processors.Dictonary[message.TopicPartition];
            await processor.OnMessage(message).ConfigureAwait(false);
        }

        protected virtual void OnOffsetsCommitted([NotNull] CommittedOffsets e)
        {
            if (e.Error.IsError || e.Error.IsFatal)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Group [{group}]: Failed to commit offsets: [{offsets}], error: {error}", Group, string.Join(", ", e.Offsets), e.Error.Reason);
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Group [{group}]: Successfully committed offsets: [{offsets}]", Group, string.Join(", ", e.Offsets));
                }
            }
        }

        protected virtual async ValueTask OnClose()
        {
            foreach (var processor in _processors.Dictonary.Values)
            {
                await processor.OnClose().ConfigureAwait(false);
            }
        }

        protected virtual void OnStatistics(string json)
        {
            _logger.LogTrace("Group [{group}]: Statistics: {statistics}", Group, json);
        }

        #region Implementation of IKafkaCoordinator

        public void Commit(TopicPartitionOffset offset)
        {
            _logger.LogDebug("Group [{group}]: Commit offset: {offset}", Group, offset);
            _consumer.Commit(new[] { offset });
        }

        #endregion
    }
}