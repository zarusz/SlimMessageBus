namespace SlimMessageBus.Host.Redis;

using Microsoft.Extensions.Logging;

using SlimMessageBus.Host.Collections;

using StackExchange.Redis;

public class RedisMessageBus : MessageBusBase
{
    private readonly ILogger _logger;
    private readonly KindMapping _kindMapping = new();
    private readonly List<IRedisConsumer> _consumers = new();

    public RedisMessageBusSettings ProviderSettings { get; }

    protected IConnectionMultiplexer Connection { get; private set; }
    protected IDatabase Database { get; private set; }

    public RedisMessageBus(MessageBusSettings settings, RedisMessageBusSettings providerSettings)
        : base(settings)
    {
        _logger = LoggerFactory.CreateLogger<RedisMessageBus>();
        ProviderSettings = providerSettings ?? throw new ArgumentNullException(nameof(providerSettings));

        OnBuildProvider();
    }

    protected override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrEmpty(ProviderSettings.ConnectionString))
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(RedisMessageBusSettings)}.{nameof(RedisMessageBusSettings.ConnectionString)} must be set");
        }

        if (ProviderSettings.EnvelopeSerializer is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(RedisMessageBusSettings)}.{nameof(RedisMessageBusSettings.EnvelopeSerializer)} must be set");
        }
    }

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
            _logger.LogWarning(e, "Error occured while executing hook {HookName}", nameof(RedisMessageBusSettings.OnDatabaseConnected));
        }
    }

    private void Connection_ErrorMessage(object sender, RedisErrorEventArgs e)
    {
        _logger.LogError("Redis recieved error message: {ErrorMessage}", e.Message);
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

    protected override async ValueTask DisposeAsyncCore()
    {
        await base.DisposeAsyncCore();

        await ((IAsyncDisposable)Connection).DisposeSilently(nameof(ConnectionMultiplexer), _logger);
    }

    #endregion

    protected override async Task OnStart()
    {
        await base.OnStart().ConfigureAwait(false);

        CreateConsumers();

        foreach (var consumer in _consumers)
        {
            await consumer.Start().ConfigureAwait(false);
        }
    }

    protected override async Task OnStop()
    {
        await base.OnStop().ConfigureAwait(false);

        foreach (var consumer in _consumers)
        {
            await consumer.Stop().ConfigureAwait(false);
        }

        await DestroyConsumers().ConfigureAwait(false);
    }

    protected void CreateConsumers()
    {
        var subscriber = Connection.GetSubscriber();

        var queues = new List<(string, IMessageProcessor<MessageWithHeaders>)>();

        object MessageProvider(Type messageType, MessageWithHeaders transportMessage) => Serializer.Deserialize(messageType, transportMessage.Payload);

        _logger.LogInformation("Creating consumers");
        foreach (var ((path, pathKind), consumerSettings) in Settings.Consumers.GroupBy(x => (x.Path, x.PathKind)).ToDictionary(x => x.Key, x => x.ToList()))
        {
            IMessageProcessor<MessageWithHeaders> processor = new ConsumerInstanceMessageProcessor<MessageWithHeaders>(consumerSettings, this, MessageProvider, path);

            var instances = consumerSettings.Max(x => x.Instances);
            if (instances > 1)
            {
                var minInstances = consumerSettings.Max(x => x.Instances);
                if (minInstances != instances)
                {
                    _logger.LogWarning($"The consumers on path {{Path}} have different number of concurrent instances (see setting {nameof(ConsumerSettings.Instances)})", path);
                }

                // When it was requested to have more than once concurrent instances working then we need to fan out the incoming Redis consumption tasks
                processor = new ConcurrencyIncreasingMessageProcessorDecorator<MessageWithHeaders>(instances, this, processor);
            }

            _logger.LogInformation(
                pathKind == PathKind.Topic
                    ? "Creating consumer for redis channel {Path}"
                    : "Creating consumer for redis list {Path}",
                path);
            if (pathKind == PathKind.Topic)
            {
                AddTopicConsumer(path, subscriber, processor);
            }
            else
            {
                queues.Add((path, processor));
            }
        }

        if (Settings.RequestResponse != null)
        {
            _logger.LogInformation(
                Settings.RequestResponse.PathKind == PathKind.Topic
                    ? "Creating response consumer for redis channel {Path}"
                    : "Creating response consumer for redis list {Path}",
                Settings.RequestResponse.Path);

            if (Settings.RequestResponse.PathKind == PathKind.Topic)
            {
                AddTopicConsumer(Settings.RequestResponse.Path, subscriber, new ResponseMessageProcessor<MessageWithHeaders>(Settings.RequestResponse, this, messageProvider: m => m.Payload));
            }
            else
            {
                queues.Add((Settings.RequestResponse.Path, new ResponseMessageProcessor<MessageWithHeaders>(Settings.RequestResponse, this, messageProvider: m => m.Payload)));
            }
        }

        if (queues.Count > 0)
        {
            _consumers.Add(new RedisListCheckerConsumer(LoggerFactory.CreateLogger<RedisListCheckerConsumer>(), Database, ProviderSettings.QueuePollDelay, ProviderSettings.QueuePollMaxIdle, queues, ProviderSettings.EnvelopeSerializer));
        }
    }

    protected async Task DestroyConsumers()
    {
        _logger.LogInformation("Destroying consumers");

        foreach (var consumer in _consumers)
        {
            await consumer.DisposeSilently(nameof(RedisTopicConsumer), _logger).ConfigureAwait(false);
        }
        _consumers.Clear();
    }

    protected void AddTopicConsumer(string topic, ISubscriber subscriber, IMessageProcessor<MessageWithHeaders> messageProcessor)
    {
        var consumer = new RedisTopicConsumer(LoggerFactory.CreateLogger<RedisTopicConsumer>(), topic, subscriber, messageProcessor, ProviderSettings.EnvelopeSerializer);
        _consumers.Add(consumer);
    }

    #region Overrides of MessageBusBase

    public override Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken)
    {
        var messageType = message.GetType();

        // determine the SMB topic name if its a Azure SB queue or topic
        var kind = _kindMapping.GetKind(messageType, path);

        return ProduceToTransport(messageType, message, path, messagePayload, messageHeaders, cancellationToken, kind);
    }

    #endregion

    protected virtual async Task ProduceToTransport(Type messageType, object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken, PathKind kind)
    {
        if (messageType is null) throw new ArgumentNullException(nameof(messageType));
        if (messagePayload is null) throw new ArgumentNullException(nameof(messagePayload));

        AssertActive();

        var messageWithHeaders = new MessageWithHeaders(messagePayload, messageHeaders);
        var messageWithHeadersBytes = ProviderSettings.EnvelopeSerializer.Serialize(typeof(MessageWithHeaders), messageWithHeaders);

        _logger.LogDebug(
            kind == PathKind.Topic
                ? "Producing message {Message} of type {MessageType} to redis channel {Path} with size {MessageSize}"
                : "Producing message {Message} of type {MessageType} to redis list {Path} with size {MessageSize}",
            message, messageType.Name, path, messageWithHeadersBytes.Length);

        var result = kind == PathKind.Topic
            ? await Database.PublishAsync(path, messageWithHeadersBytes).ConfigureAwait(false) // Use Redis Pub/Sub
            : await Database.ListRightPushAsync(path, messageWithHeadersBytes).ConfigureAwait(false); // Use Redis List Type (append on the right side/end of list)

        _logger.LogDebug(
            kind == PathKind.Topic
                ? "Produced message {Message} of type {MessageType} to redis channel {Path} with result {RedisResult}"
                : "Produced message {Message} of type {MessageType} to redis list {Path} with result {RedisResult}",
            message, messageType, path, result);
    }
}