using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Collections;

namespace SlimMessageBus.Host.Kafka
{
    public class KafkaGroupConsumer : IDisposable, IKafkaCommitController
    {
        private readonly ILogger _logger;

        public KafkaMessageBus MessageBus { get; }
        public string Group { get; }
        public ICollection<string> Topics { get; }

        private readonly SafeDictionaryWrapper<TopicPartition, IKafkaTopicPartitionProcessor> _processors;
        private Consumer _consumer;

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
            _consumer.OnMessage += OnMessage;
            _consumer.OnPartitionsAssigned += OnPartitionAssigned;
            _consumer.OnPartitionsRevoked += OnPartitionRevoked;
            _consumer.OnPartitionEOF += OnPartitionEndReached;
            _consumer.OnOffsetsCommitted += OnOffsetsCommitted;
            _consumer.OnStatistics += OnStatistics;
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

        protected Consumer CreateConsumer(string group)
        {
            var config = MessageBus.ProviderSettings.ConsumerConfigFactory(group);
            config[KafkaConfigKeys.Servers] = MessageBus.ProviderSettings.BrokerList;
            config[KafkaConfigKeys.ConsumerKeys.GroupId] = group;
            // ToDo: add support for auto commit
            config[KafkaConfigKeys.ConsumerKeys.EnableAutoCommit] = false;
            var consumer = MessageBus.ProviderSettings.ConsumerFactory(group, config);
            return consumer;
        }

        public void Start()
        {
            if (_consumerTask != null)
            {
                throw new MessageBusException($"Consumer for group {Group} already started");
            }

            _logger.LogInformation("Group [{0}]: Subscribing to topics: {1}", Group, string.Join(", ", Topics));
            _consumer.Subscribe(Topics);

            _consumerCts = new CancellationTokenSource();
            _consumerTask = Task.Factory.StartNew(ConsumerLoop, _consumerCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// The consumer group loop
        /// </summary>
        protected virtual void ConsumerLoop()
        {
            _logger.LogInformation("Group [{0}]: Consumer loop started", Group);
            try
            {
                var pollInterval = MessageBus.ProviderSettings.ConsumerPollInterval;
                var pollRetryInterval = MessageBus.ProviderSettings.ConsumerPollRetryInterval;

                for (var ct = _consumerCts.Token; !ct.IsCancellationRequested;)
                {
                    try
                    {
                        _logger.LogTrace("Group [{0}]: Polling consumer", Group);
                        _consumer.Poll(pollInterval);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Group [{0}]: Error occured while polling new messages (will retry in {1})", Group, pollRetryInterval);
                        Task.Delay(pollRetryInterval, _consumerCts.Token).Wait(_consumerCts.Token);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Group [{0}]: Error occured in group loop (terminated)", e, Group);
            }
            finally
            {
                _logger.LogInformation("Group [{0}]: Consumer loop finished", Group);
            }
        }

        public void Stop()
        {
            if (_consumerTask == null)
            {
                throw new MessageBusException($"Consumer for group {Group} not yet started");
            }

            _logger.LogInformation("Group [{0}]: Unassigning partitions", Group);
            _consumer.Unassign();

            _logger.LogInformation("Group [{0}]: Unsubscribing from topics", Group);
            _consumer.Unsubscribe();

            _consumerCts.Cancel();
            try
            {
                _consumerTask.Wait();
            }
            finally
            {
                _consumerTask = null;
                _consumerCts = null;
            }
        }

        protected virtual void OnPartitionAssigned(object sender, List<TopicPartition> partitions)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Group [{0}]: Assigned partitions: {1}", Group, string.Join(", ", partitions));
            }

            // Ensure processors exist for each assigned topic-partition
            partitions.ForEach(tp => _processors.GetOrAdd(tp));

            _consumer?.Assign(partitions);
        }

        protected virtual void OnPartitionRevoked(object sender, List<TopicPartition> partitions)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Group [{0}]: Revoked partitions: {1}", Group, string.Join(", ", partitions));
            }

            partitions.ForEach(tp => _processors.Dictonary[tp].OnPartitionRevoked().Wait());

            _consumer?.Unassign();
        }

        protected virtual void OnPartitionEndReached(object sender, TopicPartitionOffset offset)
        {
            _logger.LogDebug("Group [{0}]: Reached end of partition: {1}, next message will be at offset: {2}", Group, offset.TopicPartition, offset.Offset);

            var processor = _processors.Dictonary[offset.TopicPartition];
            processor.OnPartitionEndReached(offset).Wait();
        }

        protected virtual void OnMessage(object sender, Message message)
        {
            _logger.LogDebug("Group [{0}]: Received message with offset: {1}, payload size: {2}", Group, message.TopicPartitionOffset, message.Value.Length);

            var processor = _processors.Dictonary[message.TopicPartition];
            processor.OnMessage(message).Wait();
        }

        protected virtual void OnOffsetsCommitted(object sender, CommittedOffsets e)
        {
            if (e.Error)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("Group [{0}]: Failed to commit offsets: [{1}], error: {2}", Group, string.Join(", ", e.Offsets), e.Error);
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                    _logger.LogTrace("Group [{0}]: Successfully committed offsets: [{1}]", Group, string.Join(", ", e.Offsets));
            }
        }

        protected virtual void OnStatistics(object sender, string e)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Group [{0}]: Statistics: {1}", Group, e);
            }
        }

        #region Implementation of IKafkaCoordinator

        public Task Commit(TopicPartitionOffset offset)
        {
            _logger.LogDebug("Group [{0}]: Commit offset: {1}", Group, offset);
            return _consumer.CommitAsync(new List<TopicPartitionOffset> { offset });
        }

        #endregion
    }
}