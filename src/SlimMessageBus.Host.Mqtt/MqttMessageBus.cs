namespace SlimMessageBus.Host.Mqtt;

public class MqttMessageBus : MessageBusBase<MqttMessageBusSettings>
{
    private readonly ILogger _logger;
    private IManagedMqttClient _mqttClient;

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

        var clientOptions = ProviderSettings.ClientBuilder
            .Build();

        var managedClientOptions = ProviderSettings.ManagedClientBuilder
            .WithClientOptions(clientOptions)
            .Build();

        await _mqttClient.StartAsync(managedClientOptions).ConfigureAwait(false);
    }

    protected override async Task OnStop()
    {
        await base.OnStop().ConfigureAwait(false);

        if (_mqttClient != null)
        {
            await _mqttClient.StopAsync().ConfigureAwait(false);
        }
    }

    protected override async Task CreateConsumers()
    {
        _mqttClient = ProviderSettings.MqttFactory.CreateManagedMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        object MessageProvider(Type messageType, MqttApplicationMessage transportMessage) => Serializer.Deserialize(messageType, transportMessage.Payload);

        void AddTopicConsumer(string topic, IMessageProcessor<MqttApplicationMessage> messageProcessor)
        {
            _logger.LogInformation("Creating consumer for {Path}", topic);
            var consumer = new MqttTopicConsumer(LoggerFactory.CreateLogger<MqttTopicConsumer>(), topic, messageProcessor);
            AddConsumer(consumer);
        }

        _logger.LogInformation("Creating consumers");
        foreach (var (path, consumerSettings) in Settings.Consumers.GroupBy(x => x.Path).ToDictionary(x => x.Key, x => x.ToList()))
        {
            var processor = new MessageProcessor<MqttApplicationMessage>(consumerSettings, this, MessageProvider, path, responseProducer: this);
            AddTopicConsumer(path, processor);
        }

        if (Settings.RequestResponse != null)
        {
            var processor = new ResponseMessageProcessor<MqttApplicationMessage>(LoggerFactory, Settings.RequestResponse, responseConsumer: this, messagePayloadProvider: m => m.Payload);
            AddTopicConsumer(Settings.RequestResponse.Path, processor);
        }

        var topics = Consumers.Cast<MqttTopicConsumer>().Select(x => new MqttTopicFilterBuilder().WithTopic(x.Topic).Build()).ToList();
        await _mqttClient.SubscribeAsync(topics).ConfigureAwait(false);
    }

    protected override async Task DestroyConsumers()
    {
        await base.DestroyConsumers();

        if (_mqttClient != null)
        {
            _mqttClient.Dispose();
            _mqttClient = null;
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var consumer = Consumers.Cast<MqttTopicConsumer>().FirstOrDefault(x => x.Topic == arg.ApplicationMessage.Topic);
        if (consumer != null)
        {
            var headers = new Dictionary<string, object>();
            if (arg.ApplicationMessage.UserProperties != null)
            {
                foreach (var prop in arg.ApplicationMessage.UserProperties)
                {
                    headers[prop.Name] = prop.Value;
                }
            }
            return consumer.MessageProcessor.ProcessMessage(arg.ApplicationMessage, headers, CancellationToken);
        }
        return Task.CompletedTask;
    }

    protected override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders = null, CancellationToken cancellationToken = default)
    {
        var m = new MqttApplicationMessage
        {
            Payload = messagePayload,
            Topic = path
        };

        if (messageHeaders != null)
        {
            m.UserProperties = new List<MQTTnet.Packets.MqttUserProperty>(messageHeaders.Count);
            foreach (var header in messageHeaders)
            {
                m.UserProperties.Add(new(header.Key, header.Value.ToString()));
            }
        }

        var messageType = message?.GetType();
        try
        {
            var messageModifier = Settings.GetMessageModifier();
            messageModifier?.Invoke(message, m);

            if (messageType != null)
            {
                var producerSettings = GetProducerSettings(messageType);
                messageModifier = producerSettings.GetMessageModifier();
                messageModifier?.Invoke(message, m);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
        }

        await _mqttClient.EnqueueAsync(m);
    }
}