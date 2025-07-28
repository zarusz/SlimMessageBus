namespace SlimMessageBus.Host.AzureServiceBus;

using SlimMessageBus.Host.AzureServiceBus.Consumer;

public class ServiceBusMessageBus : MessageBusBase<ServiceBusMessageBusSettings>
{
    private readonly ILogger _logger;
    private ServiceBusClient _client;
    private SafeDictionaryWrapper<string, ServiceBusSender> _producerByPath;
    private ServiceBusTopologyService _topologyService;

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
        if (_topologyService != null)
        {
            await _topologyService.ProvisionTopology(); // provisioning happens asynchronously
        }
    }

    #region Overrides of MessageBusBase

    protected override void Build()
    {
        base.Build();

        if (ProviderSettings.TopologyProvisioning?.Enabled ?? false)
        {
            _topologyService = new ServiceBusTopologyService(LoggerFactory.CreateLogger<ServiceBusTopologyService>(), Settings, ProviderSettings);
            InitTaskList.Add(ProvisionTopology, CancellationToken);
        }

        _client = ProviderSettings.ClientFactory(Settings.ServiceProvider, ProviderSettings);

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

        MessageProvider<ServiceBusReceivedMessage> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], ServiceBusReceivedMessage>(t => t.Body.ToArray());

        foreach (var ((path, subscriptionName), consumerSettings) in Settings.Consumers
                .GroupBy(x => (x.Path, SubscriptionName: x.GetSubscriptionName(ProviderSettings)))
                .ToDictionary(x => x.Key, x => x.ToList()))
        {
            void InitConsumerContext(ServiceBusReceivedMessage m, ConsumerContext ctx)
            {
                ctx.SetTransportMessage(m);
                ctx.SetSubscriptionName(subscriptionName);
            }

            var topicSubscription = new TopicSubscriptionParams(path: path, subscriptionName: subscriptionName);
            var messageProcessor = new MessageProcessor<ServiceBusReceivedMessage>(
                consumerSettings,
                this,
                messageProvider: GetMessageProvider(path),
                path: path.ToString(),
                responseProducer: this,
                consumerContextInitializer: InitConsumerContext,
                consumerErrorHandlerOpenGenericType: typeof(IServiceBusConsumerErrorHandler<>));

            AddConsumerFrom(topicSubscription, messageProcessor, consumerSettings);
        }

        if (Settings.RequestResponse != null)
        {
            var path = Settings.RequestResponse.Path;

            var topicSubscription = new TopicSubscriptionParams(path, Settings.RequestResponse.GetSubscriptionName(ProviderSettings));
            var messageProcessor = new ResponseMessageProcessor<ServiceBusReceivedMessage>(
                LoggerFactory,
                Settings.RequestResponse,
                messageProvider: GetMessageProvider(path),
                PendingRequestStore,
                TimeProvider);

            AddConsumerFrom(topicSubscription, messageProcessor, [Settings.RequestResponse]);
        }
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            var transportMessage = GetTransportMessage(message, messageType, messageHeaders, path);
            var senderClient = _producerByPath.GetOrAdd(path);

            try
            {
                await senderClient.SendMessageAsync(transportMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound && _topologyService != null)
            {
                await EnsurePathExists(messageType, path);
                // Resend messages after the path has been created
                await senderClient.SendMessageAsync(transportMessage, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("Delivered message {Message} of type {MessageType} to {Path}", message, messageType?.Name, path);
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    /// <summary>
    /// When the topic or queue does not exist, we can try to create it. 
    /// This happens in cases where the path is dynamically set upon publish/send (different from default path)
    /// </summary>
    /// <param name="messageType"></param>
    /// <param name="path">topic or queue</param>
    /// <returns></returns>
    private async Task EnsurePathExists(Type messageType, string path)
    {
        var producerSettings = GetProducerSettings(messageType);
        if (producerSettings.PathKind == PathKind.Topic)
        {
            _logger.LogInformation("Topic {Path} does not exist, trying to create it", path);
            await _topologyService.TryCreateTopic(path, ProviderSettings.TopologyProvisioning.CanProducerCreateTopic);
        }
        else
        {
            _logger.LogInformation("Queue {Path} does not exist, trying to create it", path);
            await _topologyService.TryCreateQueue(path, ProviderSettings.TopologyProvisioning.CanProducerCreateQueue);
        }
    }

    internal Task SendBatchAsync<T>(string path,
                                    ServiceBusSender senderClient,
                                    IReadOnlyCollection<T> envelopes,
                                    ServiceBusMessageBatch batch,
                                    CancellationToken cancellationToken)
        where T : BulkMessageEnvelope
        => Retry.WithDelay(
            operation: async cancellationToken =>
            {
                try
                {
                    await senderClient.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound && _topologyService != null)
                {
                    var messageType = envelopes.FirstOrDefault()?.MessageType;
                    if (messageType != null)
                    {
                        await EnsurePathExists(messageType, path);
                        // Resend messages after the path has been created
                        await senderClient.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
                    }
                }

                _logger.LogDebug("Batch of {BatchSize} message(s) dispatched to {Path} ({SizeInBytes} bytes)", batch.Count, path, batch.SizeInBytes);
            },
            shouldRetry: (exception, attempt) =>
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


    public override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        AssertActive();

        var messages = envelopes
            .Select(envelope =>
            {
                var m = GetTransportMessage(envelope.Message, envelope.MessageType, envelope.Headers, path);
                return (Envelope: envelope, TransportMessage: m);
            })
            .ToList();

        var senderClient = _producerByPath.GetOrAdd(path);

        // multiple items - send in batches
        var dispatched = new List<T>(envelopes.Count);
        var inBatch = new List<T>(envelopes.Count);
        ServiceBusMessageBatch batch = null;
        try
        {
            // multiple items - send in batches
            using var it = messages.GetEnumerator();
            var advance = it.MoveNext();
            while (advance)
            {
                var item = it.Current;

                batch ??= await senderClient.CreateMessageBatchAsync(cancellationToken);
                if (batch.TryAddMessage(item.TransportMessage))
                {
                    inBatch.Add(item.Envelope);
                    advance = it.MoveNext();
                    if (advance)
                    {
                        continue;
                    }
                }

                if (batch.Count == 0)
                {
                    throw new ProducerMessageBusException($"Failed to add message {item.Envelope.Message} of Type {item.Envelope.MessageType?.Name} on Path {path} to an empty batch");
                }

                advance = false;
                await SendBatchAsync(path, senderClient, envelopes, batch, cancellationToken).ConfigureAwait(false);
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

    private ServiceBusMessage GetTransportMessage(object message, Type messageType, IDictionary<string, object> messageHeaders, string path)
    {
        OnProduceToTransport(message, messageType, path, messageHeaders);

        var transportMessage = new ServiceBusMessage();

        if (message != null)
        {
            transportMessage.Body = new BinaryData(SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, transportMessage));
        }

        // add headers
        if (messageHeaders != null)
        {
            foreach (var header in messageHeaders)
            {
                transportMessage.ApplicationProperties.Add(header.Key, header.Value);
            }
        }

        // global modifier first
        InvokeMessageModifier(message, messageType, transportMessage, ProviderSettings);
        if (messageType != null)
        {
            // local producer modifier second
            var producerSettings = GetProducerSettings(messageType);
            InvokeMessageModifier(message, messageType, transportMessage, producerSettings);
        }

        return transportMessage;
    }

    private void InvokeMessageModifier(object message, Type messageType, ServiceBusMessage m, HasProviderExtensions settings)
    {
        try
        {
            var messageModifier = settings.GetOrDefault(AsbProperties.MessageModifier);
            messageModifier?.Invoke(message, m);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
        }
    }

    #endregion
}
