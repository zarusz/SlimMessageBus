namespace SlimMessageBus.Host.RabbitMQ;

public class RabbitMqMessageBus : MessageBusBase<RabbitMqMessageBusSettings>, IRabbitMqChannel
{
    private readonly ILogger _logger;
    private IConnection _connection;

    private IModel _channel;
    private readonly object _channelLock = new();

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

        AddInit(CreateConnection());
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        foreach (var (queueName, consumers) in Settings.Consumers.GroupBy(x => x.GetQueueName()).ToDictionary(x => x.Key, x => x.ToList()))
        {
            AddConsumer(new RabbitMqConsumer(LoggerFactory, 
                channel: this, 
                queueName: queueName, 
                consumers, 
                Serializer, 
                messageBus: this, 
                ProviderSettings.HeaderValueConverter));
        }

        if (Settings.RequestResponse != null)
        {
            AddConsumer(new RabbitMqResponseConsumer(LoggerFactory, 
                channel: this, 
                queueName: Settings.RequestResponse.GetQueueName(), 
                Settings.RequestResponse, 
                this, 
                ProviderSettings.HeaderValueConverter));
        }
    }

    private async Task CreateConnection()
    {
        try
        {
            var retryCount = 3;
            for (var retry = 0; _connection == null && retry < retryCount; retry++)
            {
                try
                {
                    ProviderSettings.ConnectionFactory.AutomaticRecoveryEnabled = true;
                    ProviderSettings.ConnectionFactory.DispatchConsumersAsync = true;

                    _connection = ProviderSettings.Endpoints != null && ProviderSettings.Endpoints.Count > 0
                        ? ProviderSettings.ConnectionFactory.CreateConnection(ProviderSettings.Endpoints)
                        : ProviderSettings.ConnectionFactory.CreateConnection();
                }
                catch (global::RabbitMQ.Client.Exceptions.BrokerUnreachableException e)
                {
                    _logger.LogInformation(e, "Retrying {Retry} of {RetryCount} connection to RabbitMQ...", retry, retryCount);
                    await Task.Delay(ProviderSettings.ConnectionFactory.NetworkRecoveryInterval);
                }
            }

            lock (_channelLock)
            {
                _channel.CloseAndDispose();
                _channel = _connection.CreateModel();

                var topologyService = new RabbitMqTopologyService(LoggerFactory, _channel, Settings, ProviderSettings);

                var customAction = ProviderSettings.GetOrDefault<RabbitMqTopologyInitializer>(RabbitMqProperties.TopologyInitializer);
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

    protected override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitFinished();

        if (_channel == null)
        {
            throw new ProducerMessageBusException("The Channel is not available at this time");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var producer = GetProducerSettings(message.GetType());

        lock (_channelLock)
        {
            var messageProperties = _channel.CreateBasicProperties();
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
            var routingKey = routingKeyProvider?.Invoke(message, messageProperties) ?? string.Empty;

            // Invoke the bus level modifier
            var messagePropertiesModifier = ProviderSettings.GetMessagePropertiesModifier();
            messagePropertiesModifier?.Invoke(message, messageProperties);

            // Invoke the producer level modifier
            messagePropertiesModifier = producer.GetMessagePropertiesModifier();
            messagePropertiesModifier?.Invoke(message, messageProperties);

            _channel.BasicPublish(path, routingKey: routingKey, mandatory: false, basicProperties: messageProperties, body: messagePayload);
        }
    }
}
