namespace SlimMessageBus.Host.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

public class RabbitMqMessageBus : MessageBusBase<RabbitMqMessageBusSettings>, IRabbitMqChannel
{
    private readonly ILogger _logger;
    private readonly RabbitMqChannelManager _channelManager;

    #region IRabbitMqChannel

    public IModel Channel => _channelManager.Channel;

    public object ChannelLock => _channelManager.ChannelLock;

    #endregion

    public RabbitMqMessageBus(MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<RabbitMqMessageBus>();

        // Initialize the channel manager with a delegate to check disposal state
        _channelManager = new RabbitMqChannelManager(
            LoggerFactory,
            ProviderSettings,
            Settings,
            () => IsDisposing || IsDisposed);

        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService => new RabbitMqMessageBusSettingsValidationService(Settings, ProviderSettings);

    protected override void Build()
    {
        base.Build();

        InitTaskList.Add(() => _channelManager.InitializeConnection(CancellationToken), CancellationToken);
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        MessageProvider<BasicDeliverEventArgs> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], BasicDeliverEventArgs>(t => t.Body.ToArray());

        foreach (var (queueName, consumers) in Settings.Consumers.GroupBy(x => x.GetQueueName()).ToDictionary(x => x.Key, x => x.ToList()))
        {
            AddConsumer(new RabbitMqConsumer(LoggerFactory,
                channel: _channelManager,
                queueName: queueName,
                consumers,
                messageBus: this,
                messageProvider: GetMessageProvider(queueName),
                ProviderSettings.HeaderValueConverter));
        }

        if (Settings.RequestResponse != null)
        {
            var queueName = Settings.RequestResponse.GetQueueName();

            AddConsumer(new RabbitMqResponseConsumer(LoggerFactory,
                interceptors: Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(),
                channel: _channelManager,
                queueName: queueName,
                Settings.RequestResponse,
                messageProvider: GetMessageProvider(queueName),
                PendingRequestStore,
                TimeProvider,
                ProviderSettings.HeaderValueConverter));
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);

        _channelManager?.Dispose();
    }

    public override Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            OnProduceToTransport(message, messageType, path, messageHeaders);

            _channelManager.EnsureChannel();
            lock (_channelManager.ChannelLock)
            {
                GetTransportMessage(message, messageType, messageHeaders, path, out var messagePayload, out var messageProperties, out var routingKey);
                _channelManager.Channel.BasicPublish(path, routingKey: routingKey, mandatory: false, basicProperties: messageProperties, body: messagePayload);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    public override Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            _channelManager.EnsureChannel();
            lock (_channelManager.ChannelLock)
            {
                var batch = _channelManager.Channel.CreateBasicPublishBatch();
                foreach (var envelope in envelopes)
                {
                    GetTransportMessage(envelope.Message, envelope.MessageType, envelope.Headers, path, out var messagePayload, out var messageProperties, out var routingKey);
                    batch.Add(path, routingKey: routingKey, mandatory: false, properties: messageProperties, body: messagePayload.AsMemory());
                }
                batch.Publish();
            }

            return Task.FromResult(new ProduceToTransportBulkResult<T>(envelopes, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ProduceToTransportBulkResult<T>([], ex));
        }
    }

    private void GetTransportMessage(object message, Type messageType, IDictionary<string, object> messageHeaders, string path, out byte[] messagePayload, out IBasicProperties messageProperties, out string routingKey)
    {
        var producer = GetProducerSettings(messageType);
        messagePayload = SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, null);

        // This assumes there is a lock aquired for the Channel by the caller
        messageProperties = _channelManager.Channel.CreateBasicProperties();

        if (messageHeaders != null)
        {
            messageProperties.Headers ??= new Dictionary<string, object>();
            foreach (var header in messageHeaders)
            {
                messageProperties.Headers[header.Key] = ProviderSettings.HeaderValueConverter.ConvertTo(header.Value);
            }
        }

        // Calculate the routing key for the message (if provider set)
        var routingKeyProvider = producer.GetMessageRoutingKeyProvider(ProviderSettings);
        routingKey = routingKeyProvider?.Invoke(message, messageProperties) ?? string.Empty;

        // Invoke the bus level modifier
        var messagePropertiesModifier = ProviderSettings.GetMessagePropertiesModifier();
        messagePropertiesModifier?.Invoke(message, messageProperties);

        // Invoke the producer level modifier
        messagePropertiesModifier = producer.GetMessagePropertiesModifier();
        messagePropertiesModifier?.Invoke(message, messageProperties);
    }
}
