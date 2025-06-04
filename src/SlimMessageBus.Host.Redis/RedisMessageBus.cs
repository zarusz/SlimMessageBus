namespace SlimMessageBus.Host.Redis;

using Microsoft.Extensions.DependencyInjection;

public class RedisMessageBus : MessageBusBase<RedisMessageBusSettings>
{
    private readonly ILogger _logger;
    private readonly KindMapping _kindMapping = new();

    protected IConnectionMultiplexer Connection { get; private set; }
    protected IDatabase Database { get; private set; }

    public RedisMessageBus(MessageBusSettings settings, RedisMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
        _logger = LoggerFactory.CreateLogger<RedisMessageBus>();

        OnBuildProvider();
    }

    protected override IMessageBusSettingsValidationService ValidationService => new RedisMessageBusSettingsValidationService(Settings, ProviderSettings);

    #region Overrides of MessageBusBase

    protected override void Build()
    {
        base.Build();

        _kindMapping.Configure(Settings);

        Connection = ProviderSettings.ConnectionFactory();
        Connection.ConnectionFailed += Connection_ConnectionFailed;
        Connection.ConnectionRestored += Connection_ConnectionRestored;
        Connection.ErrorMessage += Connection_ErrorMessage;
        Connection.ConfigurationChanged += Connection_ConfigurationChanged;
        Connection.ConfigurationChangedBroadcast += Connection_ConfigurationChangedBroadcast;

        Database = Connection.GetDatabase();

        try
        {
            ProviderSettings.OnDatabaseConnected?.Invoke(Database);
        }
        catch (Exception e)
        {
            // Do nothing
            _logger.LogWarning(e, "Error occurred while executing hook {HookName}", nameof(RedisMessageBusSettings.OnDatabaseConnected));
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        await ((IAsyncDisposable)Connection).DisposeSilently(nameof(ConnectionMultiplexer), _logger);
    }

    #endregion

    private void Connection_ErrorMessage(object sender, RedisErrorEventArgs e)
    {
        _logger.LogError("Redis received error message: {ErrorMessage}", e.Message);
    }

    private void Connection_ConfigurationChangedBroadcast(object sender, EndPointEventArgs e)
    {
        _logger.LogDebug("Redis configuration changed broadcast from {EndPoint}", e.EndPoint);
    }

    private void Connection_ConfigurationChanged(object sender, EndPointEventArgs e)
    {
        _logger.LogDebug("Redis configuration changed from {EndPoint}", e.EndPoint);
    }

    private void Connection_ConnectionRestored(object sender, ConnectionFailedEventArgs e)
    {
        _logger.LogInformation("Redis connection restored - failure type {FailureType}, connection type: {ConnectionType}", e.FailureType, e.ConnectionType);
    }

    private void Connection_ConnectionFailed(object sender, ConnectionFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "Redis connection failed - failure type {FailureType}, connection type: {ConnectionType}, with message {ErrorMessage}", e.FailureType, e.ConnectionType, e.Exception?.Message ?? "(empty)");
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        var subscriber = Connection.GetSubscriber();

        var queues = new List<(string, IMessageProcessor<MessageWithHeaders>)>();

        void AddTopicConsumer(IEnumerable<AbstractConsumerSettings> consumerSettings, string topic, ISubscriber subscriber, IMessageProcessor<MessageWithHeaders> messageProcessor)
        {
            var consumer = new RedisTopicConsumer(
                LoggerFactory.CreateLogger<RedisTopicConsumer>(),
                consumerSettings,
                interceptors: Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(),
                topic,
                subscriber,
                messageProcessor,
                ProviderSettings.EnvelopeSerializer);

            AddConsumer(consumer);
        }

        MessageProvider<MessageWithHeaders> GetMessageProvider(string path)
            => SerializerProvider.GetSerializer(path).GetMessageProvider<byte[], MessageWithHeaders>(t => t.Payload);

        foreach (var ((path, pathKind), consumerSettings) in Settings.Consumers.GroupBy(x => (x.Path, x.PathKind)).ToDictionary(x => x.Key, x => x.ToList()))
        {
            IMessageProcessor<MessageWithHeaders> processor = new MessageProcessor<MessageWithHeaders>(
                consumerSettings,
                messageBus: this,
                messageProvider: GetMessageProvider(path),
                path: path,
                responseProducer: this,
                consumerErrorHandlerOpenGenericType: typeof(IRedisConsumerErrorHandler<>));

            var instances = consumerSettings.Max(x => x.Instances);
            if (instances > 1)
            {
                var minInstances = consumerSettings.Max(x => x.Instances);
                if (minInstances != instances)
                {
                    _logger.LogWarning($"The consumers on path {{Path}} have different number of concurrent instances (see setting {nameof(ConsumerSettings.Instances)})", path);
                }

                // When it was requested to have more than once concurrent instances working then we need to fan out the incoming Redis consumption tasks
                processor = new ConcurrentMessageProcessorDecorator<MessageWithHeaders>(instances, LoggerFactory, processor);
            }

            _logger.LogInformation("Creating consumer for redis {PathKind} {Path}", GetPathKindString(pathKind), path);
            if (pathKind == PathKind.Topic)
            {
                AddTopicConsumer(consumerSettings, path, subscriber, processor);
            }
            else
            {
                queues.Add((path, processor));
            }
        }

        if (Settings.RequestResponse != null)
        {
            var path = Settings.RequestResponse.Path;

            var messageProvider = GetMessageProvider(path);

            _logger.LogInformation("Creating response consumer for redis {PathKind} {Path}", GetPathKindString(Settings.RequestResponse.PathKind), path);
            if (Settings.RequestResponse.PathKind == PathKind.Topic)
            {
                AddTopicConsumer([Settings.RequestResponse], Settings.RequestResponse.Path, subscriber, new ResponseMessageProcessor<MessageWithHeaders>(LoggerFactory, Settings.RequestResponse, messageProvider, PendingRequestStore, TimeProvider));
            }
            else
            {
                queues.Add((Settings.RequestResponse.Path, new ResponseMessageProcessor<MessageWithHeaders>(LoggerFactory, Settings.RequestResponse, messageProvider, PendingRequestStore, TimeProvider)));
            }
        }

        if (queues.Count > 0)
        {
            AddConsumer(new RedisListCheckerConsumer(LoggerFactory.CreateLogger<RedisListCheckerConsumer>(), Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(), Database, ProviderSettings.QueuePollDelay, ProviderSettings.QueuePollMaxIdle, queues, ProviderSettings.EnvelopeSerializer));
        }
    }

    private static string GetPathKindString(PathKind pathKind) => pathKind == PathKind.Topic ? "channel" : "list";

    #region Overrides of MessageBusBase

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        try
        {
            var messagePayload = SerializerProvider.GetSerializer(path).Serialize(messageType, messageHeaders, message, null);

            // determine the SMB topic name if its a Azure SB queue or topic
            var kind = _kindMapping.GetKind(messageType, path);

            var messageWithHeaders = new MessageWithHeaders(messagePayload, messageHeaders);
            var messageWithHeadersBytes = ProviderSettings.EnvelopeSerializer.Serialize(typeof(MessageWithHeaders), null, messageWithHeaders, null);

            _logger.LogDebug(
                "Producing message {Message} of type {MessageType} to redis {PathKind} {Path} with size {MessageSize}",
                message, messageType.Name, GetPathKindString(kind), path, messageWithHeadersBytes.Length);

            var result = kind == PathKind.Topic
                ? await Database.PublishAsync(RedisUtils.ToRedisChannel(path), messageWithHeadersBytes).ConfigureAwait(false) // Use Redis Pub/Sub
                : await Database.ListRightPushAsync(path, messageWithHeadersBytes).ConfigureAwait(false); // Use Redis List Type (append on the right side/end of list)

            _logger.LogDebug(
                "Produced message {Message} of type {MessageType} to redis channel {PathKind} {Path} with result {RedisResult}",
                message, messageType, GetPathKindString(kind), path, result);
        }
        catch (Exception ex) when (ex is not ProducerMessageBusException && ex is not TaskCanceledException)
        {
            throw new ProducerMessageBusException(GetProducerErrorMessage(path, message, messageType, ex), ex);
        }
    }

    #endregion
}