namespace SlimMessageBus.Host.AmazonSQS;

public abstract class SqsBaseConsumer : AbstractConsumer
{
    private readonly ISqsClientProvider _clientProvider;

    // consumer settings
    private readonly int _maxMessages;
    private readonly int _visibilityTimeout;
    private readonly List<string> _messageAttributeNames;

    private Task _task;

    public SqsMessageBus MessageBus { get; }
    protected IMessageProcessor<Message> MessageProcessor { get; }
    protected ISqsHeaderSerializer HeaderSerializer { get; }

    protected SqsBaseConsumer(
        SqsMessageBus messageBus,
        ISqsClientProvider clientProvider,
        string path,
        IMessageProcessor<Message> messageProcessor,
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        ILogger logger)
        : base(logger,
               consumerSettings,
               path,
               messageBus.Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>())
    {
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        MessageBus = messageBus;
        MessageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
        HeaderSerializer = messageBus.HeaderSerializer;
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
        _messageAttributeNames = new List<string>(GetSingleValue(x => x.GetOrDefault(SqsProperties.MessageAttributes), nameof(SqsConsumerBuilderExtensions.FetchMessageAttributes)) ?? ["All"]);
    }

    private async Task<IReadOnlyCollection<Message>> ReceiveMessagesByUrl(string queueUrl)
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
        }, CancellationToken);

        return messageResponse.Messages;
    }

    private async Task<bool> DeleteMessageBatchByUrl(string queueUrl, IReadOnlyCollection<Message> messages)
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

        var deleteResponse = await _clientProvider.Client.DeleteMessageBatchAsync(deleteRequest, CancellationToken);

        // ToDo: capture failed messages
        return deleteResponse.Failed.Count > 0;
    }

    protected override Task OnStart()
    {
        Logger.LogInformation("Starting consumer for Queue: {Queue}", Path);
        _task = Run();
        return Task.CompletedTask;
    }

    protected override async Task OnStop()
    {
        Logger.LogInformation("Stopping consumer for Queue: {Queue}", Path);
        await _task.ConfigureAwait(false);
        _task = null;
    }

    protected async Task Run()
    {
        var queueUrl = MessageBus.GetQueueUrlOrException(Path);

        var messagesToDelete = new List<Message>(_maxMessages);

        while (!CancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await ReceiveMessagesByUrl(queueUrl).ConfigureAwait(false);
                foreach (var message in messages)
                {
                    var messageHeaders = message
                        .MessageAttributes
                        .ToDictionary(x => x.Key, x => HeaderSerializer.Deserialize(x.Key, x.Value));

                    var r = await MessageProcessor.ProcessMessage(message, messageHeaders, cancellationToken: CancellationToken).ConfigureAwait(false);
                    if (r.Exception != null)
                    {
                        Logger.LogError(r.Exception, "Message processing error - Queue: {Queue}, MessageId: {MessageId}", Path, message.MessageId);
                        // ToDo: DLQ handling
                        break;
                    }
                    messagesToDelete.Add(message);
                }

                if (messagesToDelete.Count > 0)
                {
                    await DeleteMessageBatchByUrl(queueUrl, messagesToDelete).ConfigureAwait(false);
                    messagesToDelete.Clear();
                }
            }
            catch (TaskCanceledException)
            {
                // ignore, need to finish
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while processing messages - Queue: {Queue}", Path);
                await Task.Delay(2000, CancellationToken).ConfigureAwait(false);
            }
        }
    }
}