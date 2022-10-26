namespace SlimMessageBus.Host.Redis;

using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Serialization;
using StackExchange.Redis;

public class RedisTopicConsumer : IRedisConsumer
{
    private readonly ILogger<RedisTopicConsumer> _logger;
    private readonly string _topic;
    private readonly ISubscriber _subscriber;
    private readonly IMessageSerializer _envelopeSerializer;
    private IMessageProcessor<MessageWithHeaders> _messageProcessor;
    private ChannelMessageQueue _channelMessageQueue;
    private CancellationTokenSource _cancellationTokenSource;

    public RedisTopicConsumer(ILogger<RedisTopicConsumer> logger, string topic, ISubscriber subscriber, IMessageProcessor<MessageWithHeaders> messageProcessor, IMessageSerializer envelopeSerializer)
    {
        _logger = logger;
        _topic = topic;
        _envelopeSerializer = envelopeSerializer;
        _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        _messageProcessor = messageProcessor;
    }

    public async Task Start()
    {
        if (_channelMessageQueue == null)
        {
            _logger.LogInformation("Subscribing to redis channel {Topic}", _topic);

            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            _channelMessageQueue = await _subscriber.SubscribeAsync(_topic);
            _channelMessageQueue.OnMessage(OnMessage);
        }
    }

    public async Task Stop()
    {
        if (_channelMessageQueue != null)
        {
            _logger.LogInformation("Unsubscribing from redis channel {Topic}", _topic);

            _cancellationTokenSource.Cancel();

            await _channelMessageQueue.UnsubscribeAsync();
            _channelMessageQueue = null;
        }
    }

    private async Task OnMessage(ChannelMessage m)
    {
        Exception exception;
        try
        {
            var messageWithHeaders = (MessageWithHeaders)_envelopeSerializer.Deserialize(typeof(MessageWithHeaders), m.Message);
            (exception, var exceptionConsumerSettings, _) = await _messageProcessor.ProcessMessage(messageWithHeaders, messageWithHeaders.Headers, _cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            exception = e;
        }
        _logger.LogError(exception, "Error occured while processing the redis pub/sub topic {Topic}", _topic);
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await Stop();

        if (_messageProcessor != null)
        {
            await _messageProcessor.DisposeAsync();
            _messageProcessor = null;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    #endregion
}