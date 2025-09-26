using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Extensions.ManagedClient;

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

        void AddTopicConsumer(IEnumerable<AbstractConsumerSettings> consumerSettings, string topic, IMessageProcessor<MqttApplicationMessage> messageProcessor)
        {
            _logger.LogInformation("Creating consumer for {Path}", topic);
            var consumer = new MqttTopicConsumer(LoggerFactory.CreateLogger<MqttTopicConsumer>(), consumerSettings, interceptors: Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(), topic, messageProcessor);
            AddConsumer(consumer);
        }

        MessageProvider<MqttApplicationMessage> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], MqttApplicationMessage>(t => t.PayloadSegment.Array);

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

        var topics = Consumers.Cast<MqttTopicConsumer>().Select(x => new MqttTopicFilterBuilder().WithTopic(x.Path).Build()).ToList();
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

    private static bool CheckTopic(string allowedTopic, string topic)
    {
        if (string.Equals(allowedTopic, topic))
            return true;
        var realTopicRegex = allowedTopic.Replace(@"/", @"\/").Replace("+", @"[a-zA-Z0-9 _.-]*").Replace("#", @"[a-zA-Z0-9 \/_#+.-]*");
        var regex = new Regex(realTopicRegex);
        return regex.IsMatch(topic);
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var consumer = Consumers.Cast<MqttTopicConsumer>().FirstOrDefault(x => CheckTopic(x.Path, arg.ApplicationMessage.Topic));
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
                transportMessage.UserProperties = new List<MQTTnet.Packets.MqttUserProperty>(messageHeaders.Count);
                foreach (var header in messageHeaders)
                {
                    transportMessage.UserProperties.Add(new(header.Key, header.Value.ToString()));
                }
            }

            if (message != null)
            {
                transportMessage.PayloadSegment = new ArraySegment<byte>(SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, transportMessage));
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
                _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
            }

            await _mqttClient.EnqueueAsync(transportMessage);
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }
}