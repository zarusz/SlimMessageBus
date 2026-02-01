namespace SlimMessageBus.Host.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

public class RabbitMqMessageBus : MessageBusBase<RabbitMqMessageBusSettings>, IRabbitMqChannel
{
    private readonly RabbitMqChannelManager _channelManager;

    #region IRabbitMqChannel

    public IChannel Channel => _channelManager.Channel;

    #endregion

    public RabbitMqMessageBus(MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
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

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            OnProduceToTransport(message, messageType, path, messageHeaders);

            _channelManager.EnsureChannel();

            // IChannel is thread-safe in v7 - no locking needed
            GetTransportMessage(message, messageType, messageHeaders, path, out var messagePayload, out var messageProperties, out var routingKey);
            await _channelManager.Channel.BasicPublishAsync(
                exchange: path,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: (BasicProperties)messageProperties,
                body: messagePayload,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    public override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            _channelManager.EnsureChannel();

            // IChannel is thread-safe in v7 - no locking needed
            foreach (var envelope in envelopes)
            {
                GetTransportMessage(envelope.Message, envelope.MessageType, envelope.Headers, path, out var messagePayload, out var messageProperties, out var routingKey);
                await _channelManager.Channel.BasicPublishAsync(
                    exchange: path,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: (BasicProperties)messageProperties,
                    body: messagePayload,
                    cancellationToken: cancellationToken);
            }

            return new ProduceToTransportBulkResult<T>(envelopes, null);
        }
        catch (Exception ex)
        {
            return new ProduceToTransportBulkResult<T>([], ex);
        }
    }

    private void GetTransportMessage(object message, Type messageType, IDictionary<string, object> messageHeaders, string path, out byte[] messagePayload, out IBasicProperties messageProperties, out string routingKey)
    {
        var producer = GetProducerSettings(messageType);
        messagePayload = SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, null);

        // IChannel is thread-safe in v7 - create properties without external locking
        var writableProperties = new BasicProperties();

        if (messageHeaders != null)
        {
            writableProperties.Headers ??= new Dictionary<string, object>();
            foreach (var header in messageHeaders)
            {
                writableProperties.Headers[header.Key] = ProviderSettings.HeaderValueConverter.ConvertTo(header.Value);
            }
        }

        // Calculate the routing key for the message (if provider set)
        var routingKeyProvider = producer.GetMessageRoutingKeyProvider(ProviderSettings);
        routingKey = routingKeyProvider?.Invoke(message, writableProperties) ?? string.Empty;

        // Invoke the bus level modifier
        var messagePropertiesModifier = ProviderSettings.GetMessagePropertiesModifier();
        messagePropertiesModifier?.Invoke(message, writableProperties);

        // Invoke the producer level modifier
        messagePropertiesModifier = producer.GetMessagePropertiesModifier();
        messagePropertiesModifier?.Invoke(message, writableProperties);

        // Return the writable properties
        messageProperties = writableProperties;
    }
}
