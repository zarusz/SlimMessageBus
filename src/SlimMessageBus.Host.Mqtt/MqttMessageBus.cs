namespace SlimMessageBus.Host.Mqtt;

using MQTTnet.Extensions.ManagedClient;

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

        object MessageProvider(Type messageType, MqttApplicationMessage transportMessage) => Serializer.Deserialize(messageType, transportMessage.PayloadSegment.Array);

        void AddTopicConsumer(string topic, IMessageProcessor<MqttApplicationMessage> messageProcessor)
        {
            _logger.LogInformation("Creating consumer for {Path}", topic);
            var consumer = new MqttTopicConsumer(LoggerFactory.CreateLogger<MqttTopicConsumer>(), topic, messageProcessor);
            AddConsumer(consumer);
        }

        foreach (var (path, consumerSettings) in Settings.Consumers.GroupBy(x => x.Path).ToDictionary(x => x.Key, x => x.ToList()))
        {
            var processor = new MessageProcessor<MqttApplicationMessage>(
                consumerSettings,
                messageBus: this,
                messageProvider: MessageProvider,
                path: path,
                responseProducer: this,
                consumerErrorHandlerOpenGenericType: typeof(IMqttConsumerErrorHandler<>));

            AddTopicConsumer(path, processor);
        }

        if (Settings.RequestResponse != null)
        {
            var processor = new ResponseMessageProcessor<MqttApplicationMessage>(
                LoggerFactory,
                Settings.RequestResponse,
                responseConsumer: this,
                messagePayloadProvider: m => m.PayloadSegment.Array);

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
            return consumer.MessageProcessor.ProcessMessage(arg.ApplicationMessage, headers, cancellationToken: CancellationToken);
        }
        return Task.CompletedTask;
    }

    protected override async Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        var messages = envelopes
            .Select(envelope =>
            {
                var messagePayload = Serializer.Serialize(envelope.MessageType, envelope.Message);

                var m = new MqttApplicationMessage
                {
                    PayloadSegment = new ArraySegment<byte>(messagePayload),
                    Topic = path
                };

                if (envelope.Headers != null)
                {
                    m.UserProperties = new List<MQTTnet.Packets.MqttUserProperty>(envelope.Headers.Count);
                    foreach (var header in envelope.Headers)
                    {
                        m.UserProperties.Add(new(header.Key, header.Value.ToString()));
                    }
                }

                var messageType = envelope.Message?.GetType();
                try
                {
                    var messageModifier = Settings.GetMessageModifier();
                    messageModifier?.Invoke(envelope.Message, m);

                    if (messageType != null)
                    {
                        var producerSettings = GetProducerSettings(messageType);
                        messageModifier = producerSettings.GetMessageModifier();
                        messageModifier?.Invoke(envelope.Message, m);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, envelope.Message);
                }

                return (Envelope: envelope, Message: m);
            });

        var dispatched = new List<T>(envelopes.Count);
        foreach (var item in messages)
        {
            try
            {
                await _mqttClient.EnqueueAsync(item.Message);
                dispatched.Add(item.Envelope);
            }
            catch (Exception ex)
            {
                return new(dispatched, ex);
            }
        }

        return new(dispatched, null);
    }
}
