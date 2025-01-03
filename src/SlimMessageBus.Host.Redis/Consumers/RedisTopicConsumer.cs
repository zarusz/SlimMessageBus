namespace SlimMessageBus.Host.Redis;

public class RedisTopicConsumer : AbstractConsumer, IRedisConsumer
{
    private readonly ISubscriber _subscriber;
    private readonly IMessageSerializer _envelopeSerializer;
    private ChannelMessageQueue _channelMessageQueue;
    private readonly IMessageProcessor<MessageWithHeaders> _messageProcessor;

    public RedisTopicConsumer(ILogger<RedisTopicConsumer> logger,
                              IEnumerable<AbstractConsumerSettings> consumerSettings,
                              IEnumerable<IAbstractConsumerInterceptor> interceptors,
                              string topic,
                              ISubscriber subscriber,
                              IMessageProcessor<MessageWithHeaders> messageProcessor,
                              IMessageSerializer envelopeSerializer)
        : base(logger,
               consumerSettings,
               path: topic,
               interceptors)
    {
        _messageProcessor = messageProcessor;
        _envelopeSerializer = envelopeSerializer;
        _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
    }

    protected override async Task OnStart()
    {
        // detect if wildcard is used
        var channel = RedisUtils.ToRedisChannel(Path);
        _channelMessageQueue = await _subscriber.SubscribeAsync(channel).ConfigureAwait(false);
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

            var r = await _messageProcessor.ProcessMessage(messageWithHeaders, messageWithHeaders.Headers, cancellationToken: CancellationToken);
            exception = r.Exception;
        }
        catch (Exception e)
        {
            exception = e;
        }
        if (exception != null)
        {
            // In the future offer better error handling support - retries + option to put failed messages on a DLQ.
            Logger.LogError(exception, "Error occurred while processing the redis channel {Topic}", Path);
        }
    }
}
