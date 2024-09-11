namespace SlimMessageBus.Host.Kafka;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;
using IConsumer = IConsumer<Ignore, byte[]>;

public class KafkaGroupConsumer : AbstractConsumer, IKafkaCommitController
{
    private readonly SafeDictionaryWrapper<TopicPartition, IKafkaPartitionConsumer> _processors;

    private IConsumer _consumer;
    private Task _consumerTask;
    private CancellationTokenSource _consumerCts;

    public KafkaMessageBusSettings ProviderSettings { get; }
    public string Group { get; }
    public IReadOnlyCollection<string> Topics { get; }

    public KafkaGroupConsumer(ILoggerFactory loggerFactory, KafkaMessageBusSettings providerSettings, string group, IReadOnlyCollection<string> topics, Func<TopicPartition, IKafkaCommitController, IKafkaPartitionConsumer> processorFactory)
        : base(loggerFactory.CreateLogger<KafkaGroupConsumer>())
    {
        ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));
        Group = group ?? throw new ArgumentNullException(nameof(group));
        Topics = topics ?? throw new ArgumentNullException(nameof(topics));

        Logger.LogInformation("Creating for Group: {Group}, Topics: {Topics}", group, string.Join(", ", topics));

        _processors = new SafeDictionaryWrapper<TopicPartition, IKafkaPartitionConsumer>(tp => processorFactory(tp, this));

        _consumer = CreateConsumer(group);
    }

    #region Implementation of IAsyncDisposable

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        if (_consumerTask != null)
        {
            await Stop().ConfigureAwait(false);
        }

        // dispose processors
        foreach (var p in _processors.ClearAndSnapshot())
        {
            p.DisposeSilently("processor", Logger);
        }

        // dispose the consumer
        _consumer?.DisposeSilently("consumer", Logger);
        _consumer = null;
    }

    #endregion

    protected IConsumer CreateConsumer(string group)
    {
        var config = new ConsumerConfig
        {
            GroupId = group,
            BootstrapServers = ProviderSettings.BrokerList
        };
        ProviderSettings.ConsumerConfig(config);

        // ToDo: add support for auto commit
        config.EnableAutoCommit = false;
        // Notify when we reach EoF, so that we can do a manual commit
        config.EnablePartitionEof = true;

        var consumer = ProviderSettings.ConsumerBuilderFactory(config)
            .SetStatisticsHandler((_, json) => OnStatistics(json))
            .SetPartitionsAssignedHandler((_, partitions) => OnPartitionAssigned(partitions))
            .SetPartitionsRevokedHandler((_, partitions) => OnPartitionRevoked(partitions))
            .SetOffsetsCommittedHandler((_, offsets) => OnOffsetsCommitted(offsets))
            .Build();

        return consumer;
    }

    protected override Task OnStart()
    {
        if (_consumerTask != null)
        {
            throw new MessageBusException($"Consumer for group {Group} already started");
        }

        _consumerCts = new CancellationTokenSource();
        _consumerTask = Task.Factory.StartNew(ConsumerLoop, _consumerCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        return Task.CompletedTask;
    }

    /// <summary>
    /// The consumer group loop
    /// </summary>
    protected async virtual Task ConsumerLoop()
    {
        Logger.LogInformation("Group [{Group}]: Subscribing to topics: {Topics}", Group, string.Join(", ", Topics));
        _consumer.Subscribe(Topics);

        Logger.LogInformation("Group [{Group}]: Consumer loop started", Group);
        try
        {
            try
            {
                for (var cancellationToken = _consumerCts.Token; !cancellationToken.IsCancellationRequested;)
                {
                    try
                    {
                        Logger.LogTrace("Group [{Group}]: Polling consumer", Group);
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
                        var pollRetryInterval = ProviderSettings.ConsumerPollRetryInterval;

                        Logger.LogError(e, "Group [{Group}]: Error occurred while polling new messages (will retry in {RetryInterval}) - {Reason}", Group, pollRetryInterval, e.Error.Reason);
                        await Task.Delay(pollRetryInterval, _consumerCts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }

            Logger.LogInformation("Group [{Group}]: Unsubscribing from topics", Group);
            _consumer.Unsubscribe();

            if (ProviderSettings.EnableCommitOnBusStop)
            {
                OnClose();
            }

            // Ensure the consumer leaves the group cleanly and final offsets are committed.
            _consumer.Close();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Group [{Group}]: Error occurred in group loop (terminated)", Group);
        }
        finally
        {
            Logger.LogInformation("Group [{Group}]: Consumer loop finished", Group);
        }
    }

    protected override async Task OnStop()
    {
        if (_consumerTask == null)
        {
            throw new MessageBusException($"Consumer for group {Group} not yet started");
        }

        _consumerCts.Cancel();
        try
        {
            await _consumerTask.ConfigureAwait(false);
        }
        finally
        {
            _consumerTask = null;

            _consumerCts.Dispose();
            _consumerCts = null;
        }
    }

    protected virtual void OnPartitionAssigned(ICollection<TopicPartition> partitions)
    {
        // Ensure processors exist for each assigned topic-partition
        foreach (var partition in partitions)
        {
            Logger.LogDebug("Group [{Group}]: Assigned partition, Topic: {Topic}, Partition: {Partition}", Group, partition.Topic, partition.Partition);

            var processor = _processors[partition];
            processor.OnPartitionAssigned(partition);
        }
    }

    protected virtual void OnPartitionRevoked(ICollection<TopicPartitionOffset> partitions)
    {
        foreach (var partition in partitions)
        {
            Logger.LogDebug("Group [{Group}]: Revoked Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", Group, partition.Topic, partition.Partition, partition.Offset);

            var processor = _processors[partition.TopicPartition];
            processor.OnPartitionRevoked();
        }
    }

    protected virtual void OnPartitionEndReached(TopicPartitionOffset offset)
    {
        Logger.LogDebug("Group [{Group}]: Reached end of partition, Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", Group, offset.Topic, offset.Partition, offset.Offset);

        var processor = _processors[offset.TopicPartition];
        processor.OnPartitionEndReached();
    }

    protected async virtual ValueTask OnMessage(ConsumeResult message)
    {
        Logger.LogDebug("Group [{Group}]: Received message with Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, payload size: {MessageSize}", Group, message.Topic, message.Partition, message.Offset, message.Message.Value?.Length ?? 0);

        var processor = _processors[message.TopicPartition];
        await processor.OnMessage(message).ConfigureAwait(false);
    }

    protected internal virtual void OnOffsetsCommitted(CommittedOffsets e)
    {
        if (e.Error.IsError || e.Error.IsFatal)
        {
            if (Logger.IsEnabled(LogLevel.Warning))
            {
                Logger.LogWarning("Group [{Group}]: Failed to commit offsets: [{Offsets}], error: {ErrorMessage}", Group, string.Join(", ", e.Offsets), e.Error.Reason);
            }
        }
        else
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Group [{Group}]: Successfully committed offsets: [{Offsets}]", Group, string.Join(", ", e.Offsets));
            }
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
        Logger.LogTrace("Group [{Group}]: Statistics: {statistics}", Group, json);
    }

    #region Implementation of IKafkaCoordinator

    public void Commit(TopicPartitionOffset offset)
    {
        Logger.LogDebug("Group [{Group}]: Commit Offset, Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", Group, offset.Topic, offset.Partition, offset.Offset);
        _consumer.Commit([offset]);
    }

    #endregion
}