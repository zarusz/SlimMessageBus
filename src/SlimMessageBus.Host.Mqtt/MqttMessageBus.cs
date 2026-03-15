namespace SlimMessageBus.Host.Mqtt;

public partial class MqttMessageBus : MessageBusBase<MqttMessageBusSettings>
{
    private readonly ILogger _logger;
    private IMqttClient _mqttClient;
    private MqttClientOptions _mqttClientOptions;

    public MqttMessageBus(MessageBusSettings settings, MqttMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<MqttMessageBus>();

        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService => new MqttMessageBusSettingsValidationService(Settings, ProviderSettings);

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    protected override async Task OnStart()
    {
        await base.OnStart().ConfigureAwait(false);

        _mqttClientOptions = ProviderSettings.ClientBuilder.Build();
        await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken).ConfigureAwait(false);
        await SubscribeAsync().ConfigureAwait(false);
    }

    protected override async Task OnStop()
    {
        await base.OnStop().ConfigureAwait(false);

        if (_mqttClient != null)
        {
            await _mqttClient.DisconnectAsync().ConfigureAwait(false);
        }
    }

    protected override async Task CreateConsumers()
    {
        _mqttClient = ProviderSettings.ClientFactory.CreateMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

        void AddTopicConsumer(IEnumerable<AbstractConsumerSettings> consumerSettings, string topic, IMessageProcessor<MqttApplicationMessage> messageProcessor)
        {
            LogCreatingConsumer(topic);
            var consumer = new MqttTopicConsumer(LoggerFactory.CreateLogger<MqttTopicConsumer>(), consumerSettings, interceptors: Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(), topic, messageProcessor);
            AddConsumer(consumer);
        }

        MessageProvider<MqttApplicationMessage> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], MqttApplicationMessage>(GetPayloadBytes);

        foreach (var (path, consumerSettings) in Settings.Consumers.GroupBy(x => x.Path).ToDictionary(x => x.Key, x => x.ToList()))
        {
            var processor = new MessageProcessor<MqttApplicationMessage>(
                consumerSettings,
                messageBus: this,
                messageProvider: GetMessageProvider(path),
                path: path,
                responseProducer: this,
                consumerErrorHandlerOpenGenericType: typeof(IMqttConsumerErrorHandler<>));

            AddTopicConsumer(consumerSettings, path, processor);
        }

        if (Settings.RequestResponse != null)
        {
            var path = Settings.RequestResponse.Path;

            var processor = new ResponseMessageProcessor<MqttApplicationMessage>(
                LoggerFactory,
                Settings.RequestResponse,
                messageProvider: GetMessageProvider(path),
                PendingRequestStore,
                TimeProvider);

            AddTopicConsumer([Settings.RequestResponse], path, processor);
        }
    }

    protected override async Task DestroyConsumers()
    {
        await base.DestroyConsumers();

        if (_mqttClient != null)
        {
            _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
            _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;
            _mqttClient.Dispose();
            _mqttClient = null;
        }
    }

    private async Task SubscribeAsync()
    {
        var consumers = Consumers.Cast<MqttTopicConsumer>().ToList();
        if (consumers.Count == 0)
        {
            return;
        }

        var subscribeOptionsBuilder = new MqttClientSubscribeOptionsBuilder();
        foreach (var consumer in consumers)
        {
            subscribeOptionsBuilder.WithTopicFilter(f => f.WithTopic(consumer.Path));
        }
        await _mqttClient.SubscribeAsync(subscribeOptionsBuilder.Build(), CancellationToken).ConfigureAwait(false);
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        if (e.Reason != MqttClientDisconnectReason.NormalDisconnection && !CancellationToken.IsCancellationRequested)
        {
            LogClientDisconnected(e.Reason);
            try
            {
                await Task.Delay(ProviderSettings.ReconnectDelay, CancellationToken).ConfigureAwait(false);
                await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken).ConfigureAwait(false);
                await SubscribeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogReconnectFailed(ex);
            }
        }
    }

    private static byte[] GetPayloadBytes(MqttApplicationMessage message)
    {
        var payload = message.Payload;
        var bytes = new byte[(int)payload.Length];
        payload.CopyTo(bytes.AsSpan());
        return bytes;
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var consumer = Consumers.Cast<MqttTopicConsumer>().FirstOrDefault(x => x.Path == arg.ApplicationMessage.Topic);
        if (consumer != null)
        {
            var headers = new Dictionary<string, object>();
            if (arg.ApplicationMessage.UserProperties != null)
            {
                foreach (var prop in arg.ApplicationMessage.UserProperties)
                {
                    headers[prop.Name] = prop.ReadValueAsString();
                }
            }
            return consumer.MessageProcessor.ProcessMessage(arg.ApplicationMessage, headers, cancellationToken: CancellationToken);
        }
        return Task.CompletedTask;
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            OnProduceToTransport(message, messageType, path, messageHeaders);

            var transportMessage = new MqttApplicationMessage
            {
                Topic = path,
            };

            if (messageHeaders != null)
            {
                transportMessage.UserProperties = new List<MqttUserProperty>(messageHeaders.Count);
                foreach (var header in messageHeaders)
                {
                    transportMessage.UserProperties.Add(new MqttUserProperty(header.Key, (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(header.Value.ToString())));
                }
            }

            if (message != null)
            {
                var payloadBytes = SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, transportMessage);
                transportMessage.Payload = new ReadOnlySequence<byte>(payloadBytes);
            }

            try
            {
                var messageModifier = Settings.GetMessageModifier();
                messageModifier?.Invoke(message, transportMessage);

                if (messageType != null)
                {
                    var producerSettings = GetProducerSettings(messageType);
                    messageModifier = producerSettings.GetMessageModifier();
                    messageModifier?.Invoke(message, transportMessage);
                }
            }
            catch (Exception e)
            {
                LogMessageModifierFailed(e, messageType, message);
            }

            await _mqttClient.PublishAsync(transportMessage, cancellationToken);
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Information,
       Message = "Creating consumer for {Path}")]
    private partial void LogCreatingConsumer(string path);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Warning,
       Message = "MQTT client disconnected with reason {Reason}. Attempting to reconnect...")]
    private partial void LogClientDisconnected(MqttClientDisconnectReason reason);

    [LoggerMessage(
       EventId = 2,
       Level = LogLevel.Error,
       Message = "Failed to reconnect MQTT client")]
    private partial void LogReconnectFailed(Exception ex);

    [LoggerMessage(
       EventId = 3,
       Level = LogLevel.Warning,
       Message = "The configured message modifier failed for message type {MessageType} and message {Message}")]
    private partial void LogMessageModifierFailed(Exception ex, Type messageType, object message);

    #endregion
}
