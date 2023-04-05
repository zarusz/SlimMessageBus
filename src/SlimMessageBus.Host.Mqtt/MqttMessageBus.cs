namespace SlimMessageBus.Host.Mqtt;

using MQTTnet.Extensions.ManagedClient;

public class MqttMessageBus : MessageBusBase<MqttMessageBusSettings>
{
    private readonly ILogger _logger;
    private IManagedMqttClient _mqttClient;
    private readonly List<MqttTopicConsumer> _consumers = new();

    public MqttMessageBus(MessageBusSettings settings, MqttMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<MqttMessageBus>();

        OnBuildProvider();
    }

    protected override void AssertSettings()
    {
        base.AssertSettings();

        if (ProviderSettings.ClientBuilder is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(MqttMessageBusSettings)}.{nameof(MqttMessageBusSettings.ClientBuilder)} must be set");
        }
        if (ProviderSettings.ManagedClientBuilder is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(MqttMessageBusSettings)}.{nameof(MqttMessageBusSettings.ManagedClientBuilder)} must be set");
        }
    }

    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    protected override async Task OnStart()
    {
        await base.OnStart().ConfigureAwait(false);

        _mqttClient = ProviderSettings.MqttFactory.CreateManagedMqttClient();
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var clientOptions = ProviderSettings.ClientBuilder
            .Build();

        var managedClientOptions = ProviderSettings.ManagedClientBuilder
            .WithClientOptions(clientOptions)
            .Build();

        await CreateConsumers();

        await _mqttClient.StartAsync(managedClientOptions).ConfigureAwait(false);
    }

    protected async Task CreateConsumers()
    {
        object MessageProvider(Type messageType, MqttApplicationMessage transportMessage) => Serializer.Deserialize(messageType, transportMessage.Payload);

        void AddTopicConsumer(string topic, IMessageProcessor<MqttApplicationMessage> messageProcessor)
        {
            _logger.LogInformation("Creating consumer for {Path}", topic);
            var consumer = new MqttTopicConsumer(LoggerFactory.CreateLogger<MqttTopicConsumer>(), topic, messageProcessor);
            _consumers.Add(consumer);
        }

        _logger.LogInformation("Creating consumers");
        foreach (var (path, consumerSettings) in Settings.Consumers.GroupBy(x => x.Path).ToDictionary(x => x.Key, x => x.ToList()))
        {
            var processor = new ConsumerInstanceMessageProcessor<MqttApplicationMessage>(consumerSettings, this, MessageProvider, path);
            AddTopicConsumer(path, processor);
        }

        if (Settings.RequestResponse != null)
        {
            var processor = new ResponseMessageProcessor<MqttApplicationMessage>(Settings.RequestResponse, this, messageProvider: m => m.Payload);
            AddTopicConsumer(Settings.RequestResponse.Path, processor);
        }

        var topics = _consumers.Select(x => new MqttTopicFilterBuilder().WithTopic(x.Topic).Build()).ToList();
        await _mqttClient.SubscribeAsync(topics).ConfigureAwait(false);
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var consumer = _consumers.FirstOrDefault(x => x.Topic == arg.ApplicationMessage.Topic);
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

    protected override async Task OnStop()
    {
        if (_mqttClient != null)
        {
            await _mqttClient.StopAsync().ConfigureAwait(false);
        }

        foreach(var consumer in _consumers)
        {
            await consumer.Stop();
        }
        _consumers.Clear();

        if (_mqttClient != null)
        {
            _mqttClient.Dispose();
            _mqttClient = null;
        }

        await base.OnStop().ConfigureAwait(false);
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