namespace SlimMessageBus.Host.RabbitMQ;

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
            const int retryCount = 3;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            await Retry.WithDelay(operation: async (cancellationTask) =>
                {
                    // See https://www.rabbitmq.com/client-libraries/dotnet-api-guide#connection-recovery
                    ProviderSettings.ConnectionFactory.AutomaticRecoveryEnabled = true;
                    ProviderSettings.ConnectionFactory.DispatchConsumersAsync = true;

                    _connection = ProviderSettings.Endpoints != null && ProviderSettings.Endpoints.Count > 0
                        ? ProviderSettings.ConnectionFactory.CreateConnection(ProviderSettings.Endpoints)
                        : ProviderSettings.ConnectionFactory.CreateConnection();
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

    protected override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        await EnsureInitFinished();

        if (_channel == null)
        {
            throw new ProducerMessageBusException("The Channel is not available at this time");
        }

        var dispatched = new List<T>(envelopes.Count);
        try
        {
            foreach (var envelope in envelopes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var producer = GetProducerSettings(envelope.Message.GetType());
                var messagePayload = Serializer.Serialize(envelope.MessageType, envelope.Message);

                lock (_channelLock)
                {
                    var messageProperties = _channel.CreateBasicProperties();
                    if (envelope.Headers != null)
                    {
                        messageProperties.Headers ??= new Dictionary<string, object>();
                        foreach (var header in envelope.Headers)
                        {
                            messageProperties.Headers[header.Key] = ProviderSettings.HeaderValueConverter.ConvertTo(header.Value);
                        }
                    }

                    // Calculate the routing key for the message (if provider set)
                    var routingKeyProvider = producer.GetMessageRoutingKeyProvider(ProviderSettings);
                    var routingKey = routingKeyProvider?.Invoke(envelope.Message, messageProperties) ?? string.Empty;

                    // Invoke the bus level modifier
                    var messagePropertiesModifier = ProviderSettings.GetMessagePropertiesModifier();
                    messagePropertiesModifier?.Invoke(envelope.Message, messageProperties);

                    // Invoke the producer level modifier
                    messagePropertiesModifier = producer.GetMessagePropertiesModifier();
                    messagePropertiesModifier?.Invoke(envelope.Message, messageProperties);

                    _channel.BasicPublish(path, routingKey: routingKey, mandatory: false, basicProperties: messageProperties, body: messagePayload);
                    dispatched.Add(envelope);
                }
            }
        }
        catch (Exception ex)
        {
            return new(dispatched, ex);
        }

        return new(dispatched, null);
    }
}
