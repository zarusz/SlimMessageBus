namespace SlimMessageBus.Host.Kafka;

using System.Diagnostics.CodeAnalysis;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

public abstract class KafkaPartitionConsumer : IKafkaPartitionConsumer
{
    private readonly ILogger _logger;
    private readonly IKafkaCommitController _commitController;
    private readonly IMessageSerializer _headerSerializer;
    private readonly IMessageProcessor<ConsumeResult> _messageProcessor;

    private TopicPartitionOffset _lastOffset;
    private TopicPartitionOffset _lastCheckpointOffset;
    private CancellationTokenSource _cancellationTokenSource;

    private bool _disposedValue;

    protected ILoggerFactory LoggerFactory { get; }
    protected AbstractConsumerSettings[] ConsumerSettings { get; }
    public ICheckpointTrigger CheckpointTrigger { get; set; }
    public string Group { get; }
    public TopicPartition TopicPartition { get; }

    protected KafkaPartitionConsumer(ILoggerFactory loggerFactory, AbstractConsumerSettings[] consumerSettings, string group, TopicPartition topicPartition, IKafkaCommitController commitController, IMessageSerializer headerSerializer, IMessageProcessor<ConsumeResult> messageProcessor)
    {
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        _logger = loggerFactory.CreateLogger<KafkaPartitionConsumer>();

        _logger.LogInformation("Creating consumer for Group: {Group}, Topic: {Topic}, Partition: {Partition}", group, topicPartition.Topic, topicPartition.Partition);
        
        ConsumerSettings = consumerSettings ?? throw new ArgumentNullException(nameof(consumerSettings));
        Group = group;
        TopicPartition = topicPartition;

        _headerSerializer = headerSerializer;
        _commitController = commitController;
        _messageProcessor = messageProcessor;

        // ToDo: Add support for Kafka driven automatic commit
        CheckpointTrigger = CreateCheckpointTrigger();
    }

    private ICheckpointTrigger CreateCheckpointTrigger()
    {
        var f = new CheckpointTriggerFactory(LoggerFactory, (configuredCheckpoints) => $"The checkpoint settings ({nameof(BuilderExtensions.CheckpointAfter)} and {nameof(BuilderExtensions.CheckpointEvery)}) across all the consumers that use the same Topic {TopicPartition.Topic} and Group {Group} must be the same (found settings are: {string.Join(", ", configuredCheckpoints)})");
        return f.Create(ConsumerSettings);
    }

    #region IDisposable pattern

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Implementation of IKafkaTopicPartitionProcessor

    public void OnPartitionAssigned([NotNull] TopicPartition partition)
    {
        _lastCheckpointOffset = null;
        _lastOffset = null;

        CheckpointTrigger?.Reset();

        // Generate a new token source if it wasnt created or the existing one was cancelled
        if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }
    }

    public async Task OnMessage([NotNull] ConsumeResult message)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _lastOffset = message.TopicPartitionOffset;

            var messageHeaders = message.ToHeaders(_headerSerializer);
            var (lastException, consumerSettings, response, _) = await _messageProcessor.ProcessMessage(message, messageHeaders, _cancellationTokenSource.Token).ConfigureAwait(false);
            if (lastException != null)
            {
                // ToDo: Retry logic
                // The OnMessageFaulted was called at this point by the MessageProcessor.
            }

            if (CheckpointTrigger != null && CheckpointTrigger.Increment())
            {
                Commit(message.TopicPartitionOffset);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Group [{Group}]: Error occured while consuming a message at Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", Group, message.Topic, message.Partition, message.Offset);
            throw;
        }
    }

    public void OnPartitionEndReached(TopicPartitionOffset offset)
    {
        if (CheckpointTrigger != null)
        {
            if (offset != null)
            {
                Commit(offset);
            }
        }
    }

    public void OnPartitionRevoked()
    {
        if (CheckpointTrigger != null)
        {
            _cancellationTokenSource?.Cancel();
        }
    }

    public void OnClose()
    {
        if (CheckpointTrigger != null)
        {
            Commit(_lastOffset);
            _cancellationTokenSource?.Cancel();
        }
    }

    #endregion

    public void Commit(TopicPartitionOffset offset)
    {
        if (offset != null && (_lastCheckpointOffset == null || offset.Offset > _lastCheckpointOffset.Offset))
        {
            _logger.LogDebug("Group [{Group}]: Commit at Offset: {Offset}, Partition: {Partition}, Topic: {Topic}", Group, offset.Offset, offset.Partition, offset.Topic);

            _lastCheckpointOffset = offset;
            _commitController.Commit(offset);

            CheckpointTrigger?.Reset();
        }
    }
}