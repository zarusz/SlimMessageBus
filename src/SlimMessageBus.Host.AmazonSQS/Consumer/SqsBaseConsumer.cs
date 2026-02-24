namespace SlimMessageBus.Host.AmazonSQS;

abstract internal partial class SqsBaseConsumer : AbstractConsumer
{
    private readonly ISqsClientProvider _clientProvider;
    private readonly ILogger _logger;

    // consumer settings
    private readonly int _maxMessages;
    private readonly int _visibilityTimeout;
    private readonly List<string> _messageAttributeNames;
    private readonly bool _isSubscribedToTopic;

    private Task _task;
    private CancellationTokenSource _consumerCts;

    public SqsMessageBus MessageBus { get; }
    protected IMessageProcessor<SqsTransportMessageWithPayload> MessageProcessor { get; }
    protected ISqsHeaderSerializer<Amazon.SQS.Model.MessageAttributeValue> HeaderSerializer { get; }
    protected IMessageSerializer<string> MessageSerializer { get; }

    protected SqsBaseConsumer(
        SqsMessageBus messageBus,
        ISqsClientProvider clientProvider,
        string path,
        IMessageProcessor<SqsTransportMessageWithPayload> messageProcessor,
        IMessageSerializer<string> messageSerializer,
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        ILogger logger)
        : base(logger,
               consumerSettings,
               path,
               messageBus.Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>())
    {
        _logger = logger;
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        MessageBus = messageBus;
        MessageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
        HeaderSerializer = messageBus.SqsHeaderSerializer;
        MessageSerializer = messageSerializer ?? throw new ArgumentNullException(nameof(messageSerializer));

        T GetSingleValue<T>(Func<AbstractConsumerSettings, T> selector, string settingName, T defaultValue = default)
        {
            var set = consumerSettings.Select(x => selector(x)).Where(x => x is not null && !x.Equals(defaultValue)).ToHashSet();
            if (set.Count > 1)
            {
                throw new ConfigurationMessageBusException($"All declared consumers across the same queue {path} must have the same {settingName} settings.");
            }
            return set.FirstOrDefault() ?? defaultValue;
        }

        _maxMessages = GetSingleValue(x => x.GetOrDefault(SqsProperties.MaxMessages), nameof(SqsConsumerBuilderExtensions.MaxMessages)) ?? messageBus.ProviderSettings.MaxMessageCount;
        _visibilityTimeout = GetSingleValue(x => x.GetOrDefault(SqsProperties.VisibilityTimeout), nameof(SqsConsumerBuilderExtensions.VisibilityTimeout)) ?? 30;
        _messageAttributeNames = [.. GetSingleValue(x => x.GetOrDefault(SqsProperties.MessageAttributes), nameof(SqsConsumerBuilderExtensions.FetchMessageAttributes)) ?? ["All"]];
        _isSubscribedToTopic = consumerSettings.Any(x => x.GetOrDefault(SqsProperties.SubscribeToTopic) is not null);
    }

    private async Task<IReadOnlyCollection<Message>> ReceiveMessagesByUrl(string queueUrl, CancellationToken cancellationToken)
    {
        var messageResponse = await _clientProvider.Client.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MessageAttributeNames = _messageAttributeNames,
            MaxNumberOfMessages = _maxMessages,
            VisibilityTimeout = _visibilityTimeout,
            // For information about long polling, see
            // https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html
            // Setting WaitTimeSeconds to non-zero enables long polling.
            WaitTimeSeconds = 5
        }, cancellationToken);

        return messageResponse.Messages;
    }

    private async Task<bool> DeleteMessageBatchByUrl(string queueUrl, IReadOnlyCollection<Message> messages, CancellationToken cancellationToken)
    {
        var deleteRequest = new DeleteMessageBatchRequest
        {
            QueueUrl = queueUrl,
            Entries = new List<DeleteMessageBatchRequestEntry>(messages.Count)
        };
        foreach (var message in messages)
        {
            deleteRequest.Entries.Add(new DeleteMessageBatchRequestEntry
            {
                Id = message.MessageId,
                ReceiptHandle = message.ReceiptHandle
            });
        }

        var deleteResponse = await _clientProvider.Client.DeleteMessageBatchAsync(deleteRequest, cancellationToken);

        // ToDo: capture failed messages
        return deleteResponse.Failed != null && deleteResponse.Failed.Count > 0;
    }

    protected override Task OnStart()
    {
        _consumerCts = new CancellationTokenSource();
        LogStartingConsumer(Path);
        _task = Run();
        return Task.CompletedTask;
    }

    protected override async Task OnStop()
    {
        LogStoppingConsumer(Path);
        _consumerCts?.Cancel();
        await _task.ConfigureAwait(false);
        _consumerCts?.Dispose();
        _consumerCts = null;
        _task = null;
    }

    protected async Task Run()
    {
        // Use a linked cancellation token that combines:
        // - CancellationToken from AbstractConsumer (for full Stop() lifecycle)
        // - _consumerCts.Token (for circuit breaker DoStop() calls)
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, _consumerCts.Token);
        var cancellationToken = linkedCts.Token;

        var queueMeta = await MessageBus.TopologyCache.GetMetaWithPreloadOrException(Path, PathKind.Queue, cancellationToken);
        var queueUrl = queueMeta.Url;

        var messagesToDelete = new List<Message>(_maxMessages);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await ReceiveMessagesByUrl(queueUrl, cancellationToken).ConfigureAwait(false);
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        LogReceivedMessage(Path, message.MessageId, message.Body);

                        GetPayloadAndHeadersFromMessage(message, out var messagePayload, out var messageHeaders);

                        var r = await MessageProcessor.ProcessMessage(new(message, messagePayload), messageHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (r.Exception != null)
                        {
                            LogMessageProcessingError(Path, message.MessageId, r.Exception);
                            // ToDo: DLQ handling
                            break;
                        }
                        messagesToDelete.Add(message);
                    }
                }
                if (messagesToDelete.Count > 0)
                {
                    await DeleteMessageBatchByUrl(queueUrl, messagesToDelete, cancellationToken).ConfigureAwait(false);
                    messagesToDelete.Clear();
                }
            }
            catch (TaskCanceledException)
            {
                // ignore, need to finish
            }
            catch (Exception ex)
            {
                LogErrorProcessingMessages(Path, ex);
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static readonly IReadOnlyDictionary<string, object> EmptyHeaders = new Dictionary<string, object>();

    private void GetPayloadAndHeadersFromMessage(Message message, out string messagePayload, out Dictionary<string, object> messageHeaders)
    {
        if (_isSubscribedToTopic)
        {
            // Note: Messages ariving from SNS topics are wrapped in an envelope like SnsEnvelope type. We need to get the actual message and headers from it.
            var snsEnvelope = (SnsEnvelope)MessageSerializer.Deserialize(typeof(SnsEnvelope), EmptyHeaders, message.Body, message);

            messagePayload = snsEnvelope.Message ?? throw new ConsumerMessageBusException("Message of the SNS Envelope was null");
            messageHeaders = (snsEnvelope.MessageAttributes ?? throw new ConsumerMessageBusException("Message of the SNS Envelope was null"))
                .ToDictionary(x => x.Key, x => HeaderSerializer.Deserialize(x.Key, new Amazon.SQS.Model.MessageAttributeValue { DataType = x.Value.Type, StringValue = x.Value.Value }));
        }
        else
        {
            messagePayload = message.Body;
            messageHeaders = message.MessageAttributes?
                .ToDictionary(x => x.Key, x => HeaderSerializer.Deserialize(x.Key, x.Value)) ?? new Dictionary<string, object>();
        }
    }

    #region Logging

#if NETSTANDARD2_0

    private void LogStartingConsumer(string queue)
        => _logger.LogInformation("Starting consumer for Queue: {Queue}", queue);

    private void LogStoppingConsumer(string queue)
        => _logger.LogInformation("Stopping consumer for Queue: {Queue}", queue);

    private void LogReceivedMessage(string queue, string messageId, string messagePayload)
        => _logger.LogDebug("Received message on Queue: {Queue}, MessageId: {MessageId}, Payload: {MessagePayload}", queue, messageId, messagePayload);

    private void LogMessageProcessingError(string queue, string messageId, Exception exception)
        => _logger.LogError(exception, "Message processing error - Queue: {Queue}, MessageId: {MessageId}", queue, messageId);

    private void LogErrorProcessingMessages(string queue, Exception exception)
        => _logger.LogError(exception, "Error while processing messages - Queue: {Queue}", queue);

#else

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Information,
       Message = "Starting consumer for Queue: {Queue}")]
    private partial void LogStartingConsumer(string queue);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Information,
       Message = "Stopping consumer for Queue: {Queue}")]
    private partial void LogStoppingConsumer(string queue);

    [LoggerMessage(
       EventId = 2,
       Level = LogLevel.Debug,
       Message = "Received message on Queue: {Queue}, MessageId: {MessageId}, Payload: {MessagePayload}")]
    private partial void LogReceivedMessage(string queue, string messageId, string messagePayload);

    [LoggerMessage(
       EventId = 3,
       Level = LogLevel.Error,
       Message = "Message processing error - Queue: {Queue}, MessageId: {MessageId}")]
    private partial void LogMessageProcessingError(string queue, string messageId, Exception exception);

    [LoggerMessage(
       EventId = 4,
       Level = LogLevel.Error,
       Message = "Error while processing messages - Queue: {Queue}")]
    private partial void LogErrorProcessingMessages(string queue, Exception exception);

#endif

    #endregion
}
