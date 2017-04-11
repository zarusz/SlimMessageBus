using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public abstract class KafkaGroupConsumerBase : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<KafkaGroupConsumerBase>();

        public readonly KafkaMessageBus MessageBus;
        public readonly string Group;
        public readonly List<string> Topics;

        protected Consumer Consumer;

        private Task _consumerTask;
        private CancellationTokenSource _consumerCts;

        // See https://kafka.apache.org/documentation/#newconsumerconfigs
        private static Dictionary<string, object> ConstructConfig(string brokerList, string groupId, bool enableAutoCommit) 
            => new Dictionary<string, object>
            {
                {KafkaConfigKeys.Servers, brokerList},
                {KafkaConfigKeys.Consumer.GroupId, groupId},
                {KafkaConfigKeys.Consumer.EnableAutoCommit, enableAutoCommit},
                {KafkaConfigKeys.Consumer.AutoCommitEnableMs, 5000},
                {KafkaConfigKeys.Consumer.StatisticsIntervalMs, 60000},
                {
                    "default.topic.config", new Dictionary<string, object>
                    {
                        {KafkaConfigKeys.Consumer.AutoOffsetReset, "latest"}
                    }
                }
            };

        protected KafkaGroupConsumerBase(KafkaMessageBus messageBus, string group, List<string> topics)
        {
            MessageBus = messageBus;
            Group = group;
            Topics = topics;

            // ToDo: Wrap into a factory, so that users can tweak some params
            var config = ConstructConfig(messageBus.KafkaSettings.BrokerList, group, false);
            Consumer = new Consumer(config);
            Consumer.OnMessage += OnMessage;
            Consumer.OnPartitionsAssigned += OnPartitionAssigned;
            Consumer.OnPartitionsRevoked += OnPartitionRevoked;
            Consumer.OnPartitionEOF += OnPartitionEndReached;
            Consumer.OnOffsetsCommitted += OnOffsetsCommitted;
            Consumer.OnStatistics += OnStatistics;
        }

        protected virtual void OnOffsetsCommitted(object sender, CommittedOffsets e)
        {
            if (e.Error)
            {
                Log.WarnFormat("Failed to commit offsets: [{0}], error: {1}", string.Join(", ", e.Offsets), e.Error);
            }
            else
            {
                if (Log.IsTraceEnabled)
                {
                    Log.TraceFormat("Successfully committed offsets: [{0}]", string.Join(", ", e.Offsets));
                }
            }
        }

        protected virtual void OnStatistics(object sender, string e)
        {
            if (Log.IsTraceEnabled)
            {
                Log.TraceFormat("Statistics: {0}", e);
            }
        }

        protected void Start()
        {
            if (_consumerTask != null)
            {
                throw new MessageBusException($"Consumer for group {Group} already started");
            }

            if (Log.IsInfoEnabled)
            {
                Log.InfoFormat("Subscribing to topics: {0}", string.Join(",", Topics));
            }
            Consumer.Subscribe(Topics);

            _consumerCts = new CancellationTokenSource();
            var ct = _consumerCts.Token;
            var ts = TimeSpan.FromSeconds(1);
            _consumerTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        Consumer.Poll(1000);
                    }
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Group [{0}]: Error occured while polling new messages", e, Group);
                }
            }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        protected void Stop()
        {
            if (_consumerTask == null)
            {
                throw new MessageBusException($"Consumer for group {Group} not yet started");
            }

            Log.Info("Unassigning partitions");
            Consumer.Unassign();

            Log.Info("Unsubscribing from topics");
            Consumer.Unsubscribe();

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

        public Task Commit(TopicPartitionOffset offset)
        {
            return Consumer.CommitAsync(new List<TopicPartitionOffset> { offset });
        }

        protected virtual void OnPartitionAssigned(object sender, List<TopicPartition> partitions)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Group [{0}]: Assigned partitions: {1}", Group, string.Join(", ", partitions));
            }
            Consumer?.Assign(partitions);
        }

        protected virtual void OnPartitionRevoked(object sender, List<TopicPartition> partitions)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Group [{0}]: Revoked partitions: {1}", Group, string.Join(", ", partitions));
            }
            Consumer?.Unassign();
        }

        protected virtual void OnPartitionEndReached(object sender, TopicPartitionOffset offset)
        {
            Log.DebugFormat("Group [{0}]: Reached end of topic: {1} and partition: {2}, next message will be at offset: {3}", Group, offset.Topic, offset.Partition, offset.Offset);
        }

        protected virtual void OnMessage(object sender, Message msg)
        {
            Log.DebugFormat("Group [{0}]: Received message on topic: {1} (offset: {2}, payload size: {3})", Group, msg.Topic, msg.TopicPartitionOffset, msg.Value.Length);
        }

        #region Implementation of IDisposable

        public virtual void Dispose()
        {
            if (_consumerTask != null)
            {
                Stop();
            }

            // dispose the consumer
            if (Consumer != null)
            {
                Consumer.DisposeSilently("consumer", Log);
                Consumer = null;
            }
        }

        #endregion
    }
}