namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus;

using SlimMessageBus.Host.AzureServiceBus.Consumer;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

public class ServiceBusMessageBus : MessageBusBase
{
    private readonly ILogger _logger;
    private readonly List<AsbBaseConsumer> _consumers = new();

    public ServiceBusMessageBusSettings ProviderSettings { get; }

    private ServiceBusClient _client;
    private SafeDictionaryWrapper<string, ServiceBusSender> _producerByPath;

    private Task _provisionTopologyTask = null;

    public ServiceBusMessageBus(MessageBusSettings settings, ServiceBusMessageBusSettings providerSettings)
        : base(settings)
    {
        _logger = LoggerFactory.CreateLogger<ServiceBusMessageBus>();
        ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

        OnBuildProvider();
    }

    protected override void AssertSettings()
    {
        base.AssertSettings();

        var kindMapping = new KindMapping();
        // This will validae if one path is mapped to both a topic and a queue
        kindMapping.Configure(Settings);
    }

    protected override void AssertConsumerSettings(ConsumerSettings consumerSettings)
    {
        if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

        base.AssertConsumerSettings(consumerSettings);

        Assert.IsTrue(consumerSettings.PathKind != PathKind.Topic || consumerSettings.GetSubscriptionName(required: false) != null,
            () => new ConfigurationMessageBusException($"The {nameof(ConsumerSettings)}.{nameof(SettingsExtensions.SubscriptionName)} is not set on topic {consumerSettings.Path}"));
    }

    protected void AddConsumer(TopicSubscriptionParams topicSubscription, IMessageProcessor<ServiceBusReceivedMessage> messageProcessor, IEnumerable<AbstractConsumerSettings> consumerSettings)
    {
        if (topicSubscription is null) throw new ArgumentNullException(nameof(topicSubscription));
        if (messageProcessor is null) throw new ArgumentNullException(nameof(messageProcessor));
        if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

        _logger.LogInformation("Creating consumer for Path: {Path}, SubscriptionName: {SubscriptionName}", topicSubscription.Path, topicSubscription.SubscriptionName);
        AsbBaseConsumer consumer = topicSubscription.SubscriptionName != null
            ? new AsbTopicSubscriptionConsumer(this, messageProcessor, consumerSettings, topicSubscription, _client)
            : new AsbQueueConsumer(this, messageProcessor, consumerSettings, topicSubscription, _client);

        _consumers.Add(consumer);
    }

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology();

        var prevProvisionTopologyTask = _provisionTopologyTask;
        if (prevProvisionTopologyTask != null)
        {
            // if previous provisioning is pending, skip it
            await prevProvisionTopologyTask;
        }

        if (ProviderSettings.TopologyProvisioning?.Enabled ?? false)
        {
            var provisioningService = new ServiceBusTopologyService(LoggerFactory.CreateLogger<ServiceBusTopologyService>(), Settings, ProviderSettings);

            _provisionTopologyTask = provisioningService.ProvisionTopology() // provisining happens asynchronously
                .ContinueWith(x => _provisionTopologyTask = null); // mark when it completed

            var beforeStartTask = BeforeStartTask;

            BeforeStartTask = beforeStartTask != null
                ? beforeStartTask.ContinueWith(x => _provisionTopologyTask)
                : _provisionTopologyTask;

            await _provisionTopologyTask.ConfigureAwait(false);
        }
    }

    #region Overrides of MessageBusBase

    protected override void Build()
    {
        base.Build();

        _ = ProvisionTopology();

        _client = ProviderSettings.ClientFactory();

        _producerByPath = new SafeDictionaryWrapper<string, ServiceBusSender>(path =>
        {
            _logger.LogDebug("Creating sender for path {Path}", path);
            return ProviderSettings.SenderFactory(path, _client);
        });

        static void InitConsumerContext(ServiceBusReceivedMessage m, ConsumerContext ctx) => ctx.SetTransportMessage(m);

        _logger.LogInformation("Creating consumers");

        foreach (var ((path, pathKind, subscriptionName), consumerSettings) in Settings.Consumers.GroupBy(x => (x.Path, x.PathKind, SubscriptionName: x.GetSubscriptionName(required: false))).ToDictionary(x => x.Key, x => x.ToList()))
        {
            var topicSubscription = new TopicSubscriptionParams(path: path, subscriptionName: subscriptionName);
            var messageProcessor = new ConsumerInstanceMessageProcessor<ServiceBusReceivedMessage>(consumerSettings, this, messageProvider: (messageType, m) => Serializer.Deserialize(messageType, m.Body.ToArray()), path: path.ToString(), InitConsumerContext);
            AddConsumer(topicSubscription, messageProcessor, consumerSettings);
        }

        if (Settings.RequestResponse != null)
        {
            var topicSubscription = new TopicSubscriptionParams(Settings.RequestResponse.Path, Settings.RequestResponse.GetSubscriptionName(required: false));
            var messageProcessor = new ResponseMessageProcessor<ServiceBusReceivedMessage>(Settings.RequestResponse, this, m => m.Body.ToArray());
            AddConsumer(topicSubscription, messageProcessor, new[] { Settings.RequestResponse });
        }
    }

    protected override async Task OnStart()
    {
        await base.OnStart().ConfigureAwait(false);
        await Task.WhenAll(_consumers.Select(x => x.Start())).ConfigureAwait(false);
    }

    protected override async Task OnStop()
    {
        await base.OnStop().ConfigureAwait(false);
        await Task.WhenAll(_consumers.Select(x => x.Stop())).ConfigureAwait(false);
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

        var producers = _producerByPath.ClearAndSnapshot();
        if (producers.Count > 0)
        {
            var producerCloseTasks = producers.Select(x =>
            {
                _logger.LogDebug("Closing sender client for path {Path}", x.EntityPath);
                return x.CloseAsync();
            });
            await Task.WhenAll(producerCloseTasks).ConfigureAwait(false);
        }

        if (_client != null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }

    public override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
    {
        var messageType = message?.GetType();

        AssertActive();

        _logger.LogDebug("Producing message {Message} of type {MessageType} to path {Path} with size {MessageSize}", message, messageType?.Name, path, messagePayload?.Length ?? 0);

        var m = messagePayload != null ? new ServiceBusMessage(messagePayload) : new ServiceBusMessage();

        // add headers
        if (messageHeaders != null)
        {
            foreach (var header in messageHeaders)
            {
                m.ApplicationProperties.Add(header.Key, header.Value);
            }
        }

        if (messageType != null)
        {
            var producerSettings = GetProducerSettings(messageType);

            // execute message modifier
            try
            {
                var messageModifier = producerSettings.GetMessageModifier();
                messageModifier?.Invoke(message, m);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "The configured message modifier failed for message type {MessageType} and message {Message}", messageType, message);
            }
        }

        var senderClient = _producerByPath.GetOrAdd(path);

        try
        {
            var t = _provisionTopologyTask;
            if (t != null)
            {
                // await until topology is provisioned for the first time
                await t;
            }

            await senderClient.SendMessageAsync(m, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Delivered message {Message} of type {MessageType} to {Path}", message, messageType?.Name, path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Producing message {Message} of type {MessageType} to path {Path} resulted in error {Error}", message, messageType?.Name, path, ex.Message);
            throw new ProducerMessageBusException($"Producing message {message} of type {messageType?.Name} to path {path} resulted in error: {ex.Message}", ex);
        }
    }

    public override Task ProduceRequest(object request, IDictionary<string, object> headers, string path, ProducerSettings producerSettings)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));

        return base.ProduceRequest(request, headers, path, producerSettings);
    }

    public override Task ProduceResponse(object request, IReadOnlyDictionary<string, object> requestHeaders, object response, IDictionary<string, object> responseHeaders, ConsumerSettings consumerSettings)
    {
        if (requestHeaders is null) throw new ArgumentNullException(nameof(requestHeaders));
        if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));

        return base.ProduceResponse(consumerSettings.ResponseType, requestHeaders, response, responseHeaders, consumerSettings);
    }

    #endregion
}
