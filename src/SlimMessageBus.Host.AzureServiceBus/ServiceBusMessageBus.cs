namespace SlimMessageBus.Host.AzureServiceBus;

using SlimMessageBus.Host.AzureServiceBus.Consumer;

public class ServiceBusMessageBus : MessageBusBase<ServiceBusMessageBusSettings>
{
    private readonly ILogger _logger;

    private ServiceBusClient _client;
    private SafeDictionaryWrapper<string, ServiceBusSender> _producerByPath;

    private Task _provisionTopologyTask = null;

    public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<ServiceBusMessageBus>();

        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService => new ServiceBusMessageBusSettingsValidationService(Settings, ProviderSettings);

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology();

        var provisioningService = new ServiceBusTopologyService(LoggerFactory.CreateLogger<ServiceBusTopologyService>(), Settings, ProviderSettings);
        await provisioningService.ProvisionTopology(); // provisining happens asynchronously
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

        foreach (var ((path, subscriptionName), consumerSettings) in Settings.Consumers.GroupBy(x => (x.Path, SubscriptionName: x.GetSubscriptionName(required: false))).ToDictionary(x => x.Key, x => x.ToList()))
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
            var topicSubscription = new TopicSubscriptionParams(Settings.RequestResponse.Path, Settings.RequestResponse.GetSubscriptionName(required: false));
            var messageProcessor = new ResponseMessageProcessor<ServiceBusReceivedMessage>(
                LoggerFactory,
                Settings.RequestResponse,
                responseConsumer: this,
                messagePayloadProvider: m => m.Body.ToArray());

            AddConsumerFrom(topicSubscription, messageProcessor, new[] { Settings.RequestResponse });
        }
    }

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

    protected override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
    {
        var messageType = message?.GetType();

        AssertActive();

        _logger.LogDebug("Producing message {Message} of type {MessageType} to path {Path} with size {MessageSize}", message, messageType?.Name, path, messagePayload?.Length ?? 0);

        var m = messagePayload != null ? new ServiceBusMessage(messagePayload) : new ServiceBusMessage();

        // add headers
        if (messageHeaders != null)
        {
            foreach (var header in messageHeaders)
            {
                m.ApplicationProperties.Add(header.Key, header.Value);
            }
        }

        if (messageType != null)
        {
            var producerSettings = GetProducerSettings(messageType);
            try
            {
                var messageModifier = producerSettings.GetMessageModifier();
                messageModifier?.Invoke(message, m);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
            }
        }

        var senderClient = _producerByPath.GetOrAdd(path);

        try
        {
            await EnsureInitFinished();

            await senderClient.SendMessageAsync(m, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Delivered message {Message} of type {MessageType} to {Path}", message, messageType?.Name, path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Producing message {Message} of type {MessageType} to path {Path} resulted in error {Error}", message, messageType?.Name, path, ex.Message);
            throw new ProducerMessageBusException($"Producing message {message} of type {messageType?.Name} to path {path} resulted in error: {ex.Message}", ex);
        }
    }

    public override Task ProduceRequest(object request, IDictionary<string, object> headers, string path, ProducerSettings producerSettings)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));

        return base.ProduceRequest(request, headers, path, producerSettings);
    }

    #endregion
}
