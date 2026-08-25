namespace SlimMessageBus.Host.GooglePubSub;

using System.Collections.Concurrent;

public class GooglePubSubMessageBus : MessageBusBase<GooglePubSubMessageBusSettings>
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<PublisherClient>>> _publishers = new(StringComparer.Ordinal);
    private GooglePubSubTopologyService _topologyService;

    public GooglePubSubMessageBus(MessageBusSettings settings, GooglePubSubMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<GooglePubSubMessageBus>();
        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService
        => new GooglePubSubMessageBusSettingsValidationService(Settings, ProviderSettings);

    protected override void Build()
    {
        base.Build();

        if (ProviderSettings.TopologyProvisioning?.Enabled ?? false)
        {
            if (ProviderSettings.PublisherServiceApiClientFactory == null)
            {
                throw new ConfigurationMessageBusException($"The {nameof(ProviderSettings.PublisherServiceApiClientFactory)} is not set while topology provisioning is enabled");
            }
            if (ProviderSettings.SubscriberServiceApiClientFactory == null)
            {
                throw new ConfigurationMessageBusException($"The {nameof(ProviderSettings.SubscriberServiceApiClientFactory)} is not set while topology provisioning is enabled");
            }

            InitTaskList.Add(InitializeTopology, CancellationToken);
        }
    }

    private async Task InitializeTopology()
    {
        var publisherServiceApiClient = await ProviderSettings.PublisherServiceApiClientFactory(Settings.ServiceProvider, CancellationToken).ConfigureAwait(false);
        var subscriberServiceApiClient = await ProviderSettings.SubscriberServiceApiClientFactory(Settings.ServiceProvider, CancellationToken).ConfigureAwait(false);
        _topologyService = new GooglePubSubTopologyService(
            LoggerFactory.CreateLogger<GooglePubSubTopologyService>(),
            Settings,
            ProviderSettings,
            publisherServiceApiClient,
            subscriberServiceApiClient);

        await ProvisionTopology().ConfigureAwait(false);
    }

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology().ConfigureAwait(false);
        if (_topologyService != null)
        {
            await _topologyService.Provision(CancellationToken).ConfigureAwait(false);
        }
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers().ConfigureAwait(false);

        MessageProvider<PubsubMessage> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], PubsubMessage>(message => message.Data.ToByteArray());

        void AddSubscriptionConsumer(
            string topic,
            string subscription,
            IMessageProcessor<PubsubMessage> messageProcessor,
            IEnumerable<AbstractConsumerSettings> consumerSettings)
        {
            var subscriptionName = GooglePubSubResourceName.ResolveSubscription(ProviderSettings.ProjectId, subscription);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Creating Google Pub/Sub consumer for topic {Topic}, subscription {Subscription}", topic, subscriptionName);
            }
            AddConsumer(new GooglePubSubConsumer(this, subscriptionName, messageProcessor, consumerSettings));
        }

        foreach (var group in Settings.Consumers.GroupBy(x => (Topic: x.Path, Subscription: x.GetSubscriptionName())))
        {
            var consumerSettings = group.ToList();
            var topic = group.Key.Topic;
            var subscription = group.Key.Subscription;

            void InitializeConsumerContext(PubsubMessage message, ConsumerContext context)
            {
                context.SetTransportMessage(message);
                context.SetSubscriptionName(subscription);
            }

            var processor = new MessageProcessor<PubsubMessage>(
                consumerSettings,
                this,
                messageProvider: GetMessageProvider(topic),
                path: topic,
                responseProducer: this,
                consumerContextInitializer: InitializeConsumerContext,
                consumerErrorHandlerOpenGenericType: typeof(IGooglePubSubConsumerErrorHandler<>));

            AddSubscriptionConsumer(topic, subscription, processor, consumerSettings);
        }

        if (Settings.RequestResponse != null)
        {
            var topic = Settings.RequestResponse.Path;
            var subscription = Settings.RequestResponse.GetSubscriptionName();
            var processor = new ResponseMessageProcessor<PubsubMessage>(
                LoggerFactory,
                Settings.RequestResponse,
                GetMessageProvider(topic),
                PendingRequestStore,
                TimeProvider);

            AddSubscriptionConsumer(topic, subscription, processor, [Settings.RequestResponse]);
        }
    }

    internal IReadOnlyDictionary<string, object> DeserializeHeaders(IDictionary<string, string> attributes)
    {
        if (attributes == null || attributes.Count == 0)
        {
            return new Dictionary<string, object>();
        }

        return attributes.ToDictionary(
            item => item.Key,
            item => ProviderSettings.HeaderSerializer.Deserialize(item.Key, item.Value));
    }

    private Task<PublisherClient> GetPublisher(string path)
    {
        var lazyPublisher = _publishers.GetOrAdd(path, topicPath =>
            new Lazy<Task<PublisherClient>>(
                () => ProviderSettings.PublisherClientFactory(
                    Settings.ServiceProvider,
                    GooglePubSubResourceName.ResolveTopic(ProviderSettings.ProjectId, topicPath),
                    CancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazyPublisher.Value;
    }

    private PubsubMessage CreateTransportMessage(
        object message,
        Type messageType,
        string path,
        IDictionary<string, object> messageHeaders)
    {
        OnProduceToTransport(message, messageType, path, messageHeaders);

        var transportMessage = new PubsubMessage();
        if (message != null)
        {
            var payload = SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, transportMessage);
            transportMessage.Data = ByteString.CopyFrom(payload);
        }

        if (messageHeaders != null)
        {
            foreach (var header in messageHeaders)
            {
                transportMessage.Attributes[header.Key] = ProviderSettings.HeaderSerializer.Serialize(header.Key, header.Value);
            }
        }

        InvokeModifier(message, messageType, transportMessage, ProviderSettings);
        if (messageType != null)
        {
            InvokeModifier(message, messageType, transportMessage, GetProducerSettings(messageType));
        }

        return transportMessage;
    }

    private void InvokeModifier(object message, Type messageType, PubsubMessage transportMessage, HasProviderExtensions settings)
    {
        try
        {
            settings.GetOrDefault(GooglePubSubProperties.MessageModifier)?.Invoke(message, transportMessage);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The Google Pub/Sub message modifier failed for message type {MessageType}", messageType);
        }
    }

    public override async Task ProduceToTransport(
        object message,
        Type messageType,
        string path,
        IDictionary<string, object> messageHeaders,
        IMessageBusTarget targetBus,
        CancellationToken cancellationToken)
    {
        try
        {
            var publisher = await GetPublisher(path).ConfigureAwait(false);
            var transportMessage = CreateTransportMessage(message, messageType, path, messageHeaders);
            try
            {
                await publisher.PublishAsync(transportMessage).ConfigureAwait(false);
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound && _topologyService != null)
            {
                var producerSettings = GetProducerSettings(messageType);
                await _topologyService.EnsureTopic(
                    path,
                    ProviderSettings.TopologyProvisioning.CanProducerCreateTopic,
                    [producerSettings.GetOrDefault(GooglePubSubProperties.CreateTopicOptions)],
                    cancellationToken).ConfigureAwait(false);
                await publisher.PublishAsync(transportMessage).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not ProducerMessageBusException && exception is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, exception), exception);
        }
    }

    public override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(
        IReadOnlyCollection<T> envelopes,
        string path,
        IMessageBusTarget targetBus,
        CancellationToken cancellationToken)
    {
        var dispatched = new List<T>(envelopes.Count);
        try
        {
            var publisher = await GetPublisher(path).ConfigureAwait(false);
            var publishes = envelopes.Select(async envelope =>
            {
                await publisher.PublishAsync(CreateTransportMessage(envelope.Message, envelope.MessageType, path, envelope.Headers)).ConfigureAwait(false);
                lock (dispatched)
                {
                    dispatched.Add(envelope);
                }
            });

            cancellationToken.ThrowIfCancellationRequested();
            await Task.WhenAll(publishes).ConfigureAwait(false);
            return new(dispatched, null);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Publishing a Google Pub/Sub message batch to {Topic} failed", path);
            return new(dispatched, exception);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);

        var publisherTasks = _publishers.Values.Where(x => x.IsValueCreated).Select(x => x.Value).ToList();
        _publishers.Clear();
        if (publisherTasks.Count > 0)
        {
            var publishers = await Task.WhenAll(publisherTasks).ConfigureAwait(false);
            foreach (var publisher in publishers)
            {
                await publisher.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
