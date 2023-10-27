namespace SlimMessageBus.Host.Redis;

public class RedisTopicConsumer : AbstractConsumer, IRedisConsumer
{
    private readonly ISubscriber _subscriber;
    private readonly IMessageSerializer _envelopeSerializer;
    private ChannelMessageQueue _channelMessageQueue;
    private IMessageProcessor<MessageWithHeaders> _messageProcessor;

    public string Path { get; }

    public RedisTopicConsumer(ILogger<RedisTopicConsumer> logger, string topic, ISubscriber subscriber, IMessageProcessor<MessageWithHeaders> messageProcessor, IMessageSerializer envelopeSerializer)
        : base(logger)
    {
        Path = topic;
        _messageProcessor = messageProcessor;
        _envelopeSerializer = envelopeSerializer;
        _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
    }

    protected override async Task OnStart()
    {
        _channelMessageQueue = await _subscriber.SubscribeAsync(Path).ConfigureAwait(false);
        _channelMessageQueue.OnMessage(OnMessage);
    }

    protected override async Task OnStop()
    {
        await _channelMessageQueue.UnsubscribeAsync().ConfigureAwait(false);
        _channelMessageQueue = null;
    }

    private async Task OnMessage(ChannelMessage m)
    {
        if (CancellationToken.IsCancellationRequested)
        {
            return;
        }

        Exception exception;
        try
        {
            var messageWithHeaders = (MessageWithHeaders)_envelopeSerializer.Deserialize(typeof(MessageWithHeaders), m.Message);
            (exception, var exceptionConsumerSettings, _, _) = await _messageProcessor.ProcessMessage(messageWithHeaders, messageWithHeaders.Headers, CancellationToken);
        }
        catch (Exception e)
        {
            exception = e;
        }
        if (exception != null)
        {
            // In the future offer better error handling support - retries + option to put failed messages on a DLQ.
            Logger.LogError(exception, "Error occured while processing the redis channel {Topic}", Path);
        }
    }
}