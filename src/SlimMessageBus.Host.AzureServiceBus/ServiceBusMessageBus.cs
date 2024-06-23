namespace SlimMessageBus.Host.AzureServiceBus;

using SlimMessageBus.Host.AzureServiceBus.Consumer;

public class ServiceBusMessageBus : MessageBusBase<ServiceBusMessageBusSettings>
{
    private readonly ILogger _logger;
    private ServiceBusClient _client;
    private SafeDictionaryWrapper<string, ServiceBusSender> _producerByPath;

    public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<ServiceBusMessageBus>();

        OnBuildProvider();
    }

    // Maximum number of messages per transaction (https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas)
    public override int? MaxMessagesPerTransaction => 100;

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);

        var producers = _producerByPath.ClearAndSnapshot();
        if (producers.Count > 0)
        {
            var producerCloseTasks = producers.Select(x =>
            {
                _logger.LogDebug("Closing sender client for path {Path}", x.EntityPath);
                return x.CloseAsync();
            });
            await Task.WhenAll(producerCloseTasks).ConfigureAwait(false);
        }

        if (_client != null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }

    protected override IMessageBusSettingsValidationService ValidationService => new ServiceBusMessageBusSettingsValidationService(Settings, ProviderSettings);

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology();

        var provisioningService = new ServiceBusTopologyService(LoggerFactory.CreateLogger<ServiceBusTopologyService>(), Settings, ProviderSettings);
        await provisioningService.ProvisionTopology(); // provisioning happens asynchronously
    }

    #region Overrides of MessageBusBase

    protected override void Build()
    {
        base.Build();

        if (ProviderSettings.TopologyProvisioning?.Enabled ?? false)
        {
            AddInit(ProvisionTopology());
        }

        _client = ProviderSettings.ClientFactory();

        _producerByPath = new SafeDictionaryWrapper<string, ServiceBusSender>(path =>
        {
            _logger.LogDebug("Creating sender for path {Path}", path);
            return ProviderSettings.SenderFactory(path, _client);
        });
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        void AddConsumerFrom(TopicSubscriptionParams topicSubscription, IMessageProcessor<ServiceBusReceivedMessage> messageProcessor, IEnumerable<AbstractConsumerSettings> consumerSettings)
        {
            _logger.LogInformation("Creating consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", topicSubscription.Path, topicSubscription.SubscriptionName);
            AsbBaseConsumer consumer = topicSubscription.SubscriptionName != null
                ? new AsbTopicSubscriptionConsumer(this, messageProcessor, consumerSettings, topicSubscription, _client)
                : new AsbQueueConsumer(this, messageProcessor, consumerSettings, topicSubscription, _client);

            AddConsumer(consumer);
        }

        static void InitConsumerContext(ServiceBusReceivedMessage m, ConsumerContext ctx) => ctx.SetTransportMessage(m);

        foreach (var ((path, subscriptionName), consumerSettings) in Settings.Consumers
                .GroupBy(x => (x.Path, SubscriptionName: x.GetSubscriptionName(ProviderSettings)))
                .ToDictionary(x => x.Key, x => x.ToList()))
        {
            var topicSubscription = new TopicSubscriptionParams(path: path, subscriptionName: subscriptionName);
            var messageProcessor = new MessageProcessor<ServiceBusReceivedMessage>(
                consumerSettings,
                this,
                messageProvider: (messageType, m) => Serializer.Deserialize(messageType, m.Body.ToArray()),
                path: path.ToString(),
                responseProducer: this,
                InitConsumerContext);

            AddConsumerFrom(topicSubscription, messageProcessor, consumerSettings);
        }

        if (Settings.RequestResponse != null)
        {
            var topicSubscription = new TopicSubscriptionParams(Settings.RequestResponse.Path, Settings.RequestResponse.GetSubscriptionName(ProviderSettings));
            var messageProcessor = new ResponseMessageProcessor<ServiceBusReceivedMessage>(
                LoggerFactory,
                Settings.RequestResponse,
                responseConsumer: this,
                messagePayloadProvider: m => m.Body.ToArray());

            AddConsumerFrom(topicSubscription, messageProcessor, [Settings.RequestResponse]);
        }
    }

    protected override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        Task SendBatchAsync(ServiceBusSender senderClient, ServiceBusMessageBatch batch, CancellationToken cancellationToken) =>
            Retry.WithDelay(
                async cancellationToken =>
                {
                    await senderClient.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
                    _logger.LogDebug("Batch of {BatchSize} message(s) dispatched to {Path} ({SizeInBytes} bytes)", batch.Count, path, batch.SizeInBytes);
                },
                (exception, attempt) =>
                {
                    if (attempt < 3
                        && exception is ServiceBusException ex
                        && ex.Reason == ServiceBusFailureReason.ServiceBusy)
                    {
                        _logger.LogWarning("Service bus throttled. Backing off (Attempt: {Attempt}).", attempt);
                        return true;
                    }
                    return false;
                },
                delay: TimeSpan.FromSeconds(2),
                jitter: TimeSpan.FromSeconds(1),
                cancellationToken);

        AssertActive();

        var messages = envelopes
            .Select(envelope =>
            {
                var messageType = envelope.Message?.GetType();
                var messagePayload = Serializer.Serialize(envelope.MessageType, envelope.Message);

                _logger.LogDebug("Producing item {Message} of type {MessageType} to path {Path} with size {MessageSize}", envelope.Message, messageType?.Name, path, messagePayload?.Length ?? 0);

                var m = messagePayload != null ? new ServiceBusMessage(messagePayload) : new ServiceBusMessage();

                // add headers
                if (envelope.Headers != null)
                {
                    foreach (var header in envelope.Headers)
                    {
                        m.ApplicationProperties.Add(header.Key, header.Value);
                    }
                }

                // global modifier first
                InvokeMessageModifier(envelope.Message, messageType, m, ProviderSettings);
                if (messageType != null)
                {
                    // local producer modifier second
                    var producerSettings = GetProducerSettings(messageType);
                    InvokeMessageModifier(envelope.Message, messageType, m, producerSettings);
                }

                return (Envelope: envelope, ServiceBusMessage: m);
            })
            .ToList();

        var senderClient = _producerByPath.GetOrAdd(path);

        await EnsureInitFinished();

        if (messages.Count == 1)
        {
            // only one item - quicker to send on its own
            var item = messages.Single();
            try
            {
                await senderClient.SendMessageAsync(item.ServiceBusMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Delivered item {Message} of type {MessageType} to {Path}", item.Envelope.Message, item.Envelope.MessageType?.Name, path);

                return new([item.Envelope], null);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Producing message {Message} of type {MessageType} to path {Path} resulted in error {Error}", item.Envelope.Message, item.Envelope.MessageType?.Name, path, ex.Message);
                return new([], ex);
            }
        }

        // multiple items - send in batches
        var dispatched = new List<T>(envelopes.Count);
        ServiceBusMessageBatch batch = null;
        try
        {
            // multiple items - send in batches
            var inBatch = new List<T>(envelopes.Count);
            var i = 0;
            while (i < messages.Count)
            {
                var item = messages[i];
                batch ??= await senderClient.CreateMessageBatchAsync(cancellationToken);
                if (batch.TryAddMessage(item.ServiceBusMessage))
                {
                    inBatch.Add(item.Envelope);
                    if (++i < messages.Count)
                    {
                        continue;
                    }
                }

                if (batch.Count == 0)
                {
                    throw new ProducerMessageBusException($"Failed to add message {item.Envelope.Message} of Type {item.Envelope.MessageType?.Name} on Path {path} to an empty batch");
                }

                await SendBatchAsync(senderClient, batch, cancellationToken).ConfigureAwait(false);

                dispatched.AddRange(inBatch);
                inBatch.Clear();
                batch.Dispose();
                batch = null;
            }

            return new(dispatched, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Producing message batch to path {Path} resulted in error {Error}", path, ex.Message);
            return new(dispatched, ex);
        }
        finally
        {
            batch?.Dispose();
        }
    }

    private void InvokeMessageModifier(object message, Type messageType, ServiceBusMessage m, HasProviderExtensions settings)
    {
        try
        {
            var messageModifier = settings.GetMessageModifier();
            messageModifier?.Invoke(message, m);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
        }
    }

    #endregion
}
