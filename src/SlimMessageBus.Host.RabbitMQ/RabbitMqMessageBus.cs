namespace SlimMessageBus.Host.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

public class RabbitMqMessageBus : MessageBusBase<RabbitMqMessageBusSettings>, IRabbitMqChannel
{
    private readonly ILogger _logger;
    private IConnection _connection;

    private readonly object _channelLock = new();
    private IModel _channel;

    #region IRabbitMqChannel

    public IModel Channel => _channel;

    public object ChannelLock => _channelLock;

    #endregion

    public RabbitMqMessageBus(MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<RabbitMqMessageBus>();

        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService => new RabbitMqMessageBusSettingsValidationService(Settings, ProviderSettings);

    protected override void Build()
    {
        base.Build();

        InitTaskList.Add(CreateConnection, CancellationToken);
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        foreach (var (queueName, consumers) in Settings.Consumers.GroupBy(x => x.GetQueueName()).ToDictionary(x => x.Key, x => x.ToList()))
        {
            var messageSerializer = SerializerProvider.GetSerializer(queueName);
            object MessageProvider(Type messageType, BasicDeliverEventArgs transportMessage) => messageSerializer.Deserialize(messageType, transportMessage.Body.ToArray());

            AddConsumer(new RabbitMqConsumer(LoggerFactory,
                channel: this,
                queueName: queueName,
                consumers,
                messageBus: this,
                MessageProvider,
                ProviderSettings.HeaderValueConverter));
        }

        if (Settings.RequestResponse != null)
        {
            var queueName = Settings.RequestResponse.GetQueueName();

            var messageSerializer = SerializerProvider.GetSerializer(queueName);
            object MessageProvider(Type messageType, BasicDeliverEventArgs transportMessage) => messageSerializer.Deserialize(messageType, transportMessage.Body.ToArray());

            AddConsumer(new RabbitMqResponseConsumer(LoggerFactory,
                interceptors: Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(),
                channel: this,
                queueName: queueName,
                Settings.RequestResponse,
                MessageProvider,
                PendingRequestStore,
                TimeProvider,
                ProviderSettings.HeaderValueConverter));
        }
    }

    private async Task CreateConnection()
    {
        try
        {
            const int retryCount = 3;
            await Retry.WithDelay(
                operation: (ct) =>
                {
                    // See https://www.rabbitmq.com/client-libraries/dotnet-api-guide#connection-recovery
                    ProviderSettings.ConnectionFactory.AutomaticRecoveryEnabled = true;
                    ProviderSettings.ConnectionFactory.DispatchConsumersAsync = true;

                    _connection = ProviderSettings.Endpoints != null && ProviderSettings.Endpoints.Count > 0
                        ? ProviderSettings.ConnectionFactory.CreateConnection(ProviderSettings.Endpoints)
                        : ProviderSettings.ConnectionFactory.CreateConnection();

                    return Task.CompletedTask;
                },
                shouldRetry: (ex, attempt) =>
                {
                    if (ex is global::RabbitMQ.Client.Exceptions.BrokerUnreachableException && attempt < retryCount)
                    {
                        _logger.LogInformation(ex, "Retrying {Retry} of {RetryCount} connection to RabbitMQ...", attempt, retryCount);
                        return true;
                    }
                    return false;
                },
                delay: ProviderSettings.ConnectionFactory.NetworkRecoveryInterval
            );
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

            lock (_channelLock)
            {
                _channel?.CloseAndDispose();

                if (_connection != null)
                {
                    _channel = _connection.CreateModel();

                    var topologyService = new RabbitMqTopologyService(LoggerFactory, _channel, Settings, ProviderSettings);

                    var customAction = ProviderSettings.GetOrDefault(RabbitMqProperties.TopologyInitializer);
                    if (customAction != null)
                    {
                        // Allow the user to specify its own initializer
                        customAction(_channel, () => topologyService.ProvisionTopology());
                    }
                    else
                    {
                        // Perform default topology setup
                        topologyService.ProvisionTopology();
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not initialize RabbitMQ connection: {ErrorMessage}", e.Message);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);

        if (_channel != null)
        {
            _channel.CloseAndDispose();
            _channel = null;
        }

        if (_connection != null)
        {
            _connection.CloseAndDispose();
            _connection = null;
        }
    }

    public override Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        EnsureChannel();
        try
        {
            OnProduceToTransport(message, messageType, path, messageHeaders);

            lock (_channelLock)
            {
                GetTransportMessage(message, messageType, messageHeaders, path, out var messagePayload, out var messageProperties, out var routingKey);
                _channel.BasicPublish(path, routingKey: routingKey, mandatory: false, basicProperties: messageProperties, body: messagePayload);
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
        EnsureChannel();

        try
        {
            lock (_channelLock)
            {
                var batch = _channel.CreateBasicPublishBatch();
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

    private void EnsureChannel()
    {
        if (_channel == null)
        {
            throw new ProducerMessageBusException("The Channel is not available at this time");
        }
    }

    private void GetTransportMessage(object message, Type messageType, IDictionary<string, object> messageHeaders, string path, out byte[] messagePayload, out IBasicProperties messageProperties, out string routingKey)
    {
        var producer = GetProducerSettings(messageType);
        messagePayload = SerializerProvider.GetSerializer(path).Serialize(messageType, message);
        messageProperties = _channel.CreateBasicProperties();
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
