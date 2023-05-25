namespace SlimMessageBus.Host.RabbitMQ;

public class RabbitMqMessageBus : MessageBusBase<RabbitMqMessageBusSettings>, IRabbitMqChannel
{
    private readonly ILogger _logger;
    private IConnection _connection;

    private IModel _channel;
    private readonly object _channelLock = new();

    private readonly IList<AbstractConsumer> _consumers = new List<AbstractConsumer>();

    private Task _initTask;

    #region IRabbitMqChannel

    public IModel Channel => _channel;

    public object ChannelLock => _channelLock;

    #endregion

    public RabbitMqMessageBus(MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<RabbitMqMessageBus>();

        OnBuildProvider();
    }

    protected override void AssertProducers()
    {
        foreach (var producer in Settings.Producers)
        {
            var exchangeName = producer.DefaultPath;
            if (exchangeName == null)
            {
                throw new ConfigurationMessageBusException($"The {nameof(RabbitMqProducerBuilderExtensions.Exchange)} is not provided on the producer for message type {producer.MessageType.Name}");
            }

            var routingKeyProvider = producer.GetMessageRoutingKeyProvider(ProviderSettings);
            if (routingKeyProvider == null)
            {
                // Requirement for the routing key depends on the ExchangeType
                var exchangeType = producer.GetExchageType(ProviderSettings);
                if (exchangeType == global::RabbitMQ.Client.ExchangeType.Direct || exchangeType == global::RabbitMQ.Client.ExchangeType.Topic)
                {
                    throw new ConfigurationMessageBusException($"The {nameof(RabbitMqProducerBuilderExtensions.RoutingKeyProvider)} is neither provided on the producer for exchange {producer.DefaultPath} nor a default provider exists at the bus level (check that .{nameof(RabbitMqProducerBuilderExtensions.RoutingKeyProvider)}() exists on the producer or bus level). Exchange type {exchangeType} requires the producer to has a routing key provider.");
                }
            }
        }

        base.AssertProducers();
    }

    protected override void AssertConsumerSettings(ConsumerSettings consumerSettings)
    {
        var exchangeName = consumerSettings.Path;
        if (exchangeName == null)
        {
            throw new ConfigurationMessageBusException($"The {nameof(RabbitMqConsumerBuilderExtensions.ExchangeBinding)} is not provided on the consumer for message type {consumerSettings.MessageType.Name}");
        }

        var queueName = consumerSettings.GetQueueName();
        if (queueName == null)
        {
            throw new ConfigurationMessageBusException($"The {nameof(RabbitMqConsumerBuilderExtensions.Queue)} is not provided on the consumer for message type {consumerSettings.MessageType.Name}");
        }

        base.AssertConsumerSettings(consumerSettings);
    }

    protected override void AssertRequestResponseSettings()
    {
        if (Settings.RequestResponse != null)
        {
            var exchangeName = Settings.RequestResponse.Path;
            if (exchangeName == null)
            {
                throw new ConfigurationMessageBusException($"The {nameof(RabbitMqRequestResponseBuilderExtensions.ReplyToExchange)} is not provided on the {nameof(MessageBusBuilder.ExpectRequestResponses)}()");
            }

            var queueName = Settings.RequestResponse.GetQueueName();
            if (queueName == null)
            {
                throw new ConfigurationMessageBusException($"The {nameof(RabbitMqConsumerBuilderExtensions.Queue)} is not provided on the {nameof(MessageBusBuilder.ExpectRequestResponses)}()");
            }
        }
        base.AssertRequestResponseSettings();
    }

    protected override void Build()
    {
        base.Build();

        foreach (var (queueName, consumers) in Settings.Consumers.GroupBy(x => x.GetQueueName()).ToDictionary(x => x.Key, x => x.ToList()))
        {
            _consumers.Add(new RabbitMqConsumer(LoggerFactory, channel: this, queueName: queueName, consumers, Serializer, messageBus: this, ProviderSettings.HeaderValueConverter));
        }

        if (Settings.RequestResponse != null)
        {
            _consumers.Add(new RabbitMqResponseConsumer(LoggerFactory, channel: this, queueName: Settings.RequestResponse.GetQueueName(), Settings.RequestResponse, this, ProviderSettings.HeaderValueConverter));
        }

        _initTask = Task.Factory.StartNew(CreateConnection).Unwrap();
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
        finally
        {
            _initTask = null;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore().ConfigureAwait(false);

        if (_consumers.Count > 0)
        {
            foreach (var consumer in _consumers)
            {
                await consumer.DisposeSilently("Consumer", _logger).ConfigureAwait(false);
            }
            _consumers.Clear();
        }

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

    protected override void AssertSettings()
    {
        base.AssertSettings();

        if (ProviderSettings.ConnectionFactory is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(RabbitMqMessageBusSettings)}.{nameof(RabbitMqMessageBusSettings.ConnectionFactory)} must be set");
        }
    }

    protected override async Task OnStart()
    {
        if (_initTask != null)
        {
            await _initTask.ConfigureAwait(false);
        }

        await base.OnStart().ConfigureAwait(false);
        await Task.WhenAll(_consumers.Select(x => x.Start())).ConfigureAwait(false);
    }

    protected override async Task OnStop()
    {
        if (_initTask != null)
        {
            await _initTask.ConfigureAwait(false);
        }

        await base.OnStop().ConfigureAwait(false);
        await Task.WhenAll(_consumers.Select(x => x.Stop())).ConfigureAwait(false);
    }

    protected override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders = null, CancellationToken cancellationToken = default)
    {
        if (_initTask != null)
        {
            await _initTask.ConfigureAwait(false);
        }

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
