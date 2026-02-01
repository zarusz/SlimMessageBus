namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

public abstract class AbstractRabbitMqConsumer : AbstractConsumer
{
    private readonly IRabbitMqChannel _channel;
    private readonly IHeaderValueConverter _headerValueConverter;
    private AsyncEventingBasicConsumer _consumer;
    private string _consumerTag;
    private readonly object _consumerLock = new();

    protected abstract RabbitMqMessageAcknowledgementMode AcknowledgementMode { get; }

    protected AbstractRabbitMqConsumer(ILogger logger,
                                       IEnumerable<AbstractConsumerSettings> consumerSettings,
                                       IEnumerable<IAbstractConsumerInterceptor> interceptors,
                                       IRabbitMqChannel channel,
                                       string queueName,
                                       IHeaderValueConverter headerValueConverter)
        : base(logger,
               consumerSettings,
               path: queueName,
               interceptors)
    {
        _channel = channel;
        _headerValueConverter = headerValueConverter;

        // Subscribe to channel recovery events
        if (_channel is RabbitMqChannelManager channelManager)
        {
            channelManager.ChannelRecovered += OnChannelRecovered;
        }
    }

    private async void OnChannelRecovered(object sender, EventArgs e)
    {
        Logger.LogInformation("Channel recovered, re-registering consumer for queue {QueueName}", Path);

        try
        {
            // Re-register the consumer
            await ReRegisterConsumer();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to re-register consumer for queue {QueueName} after channel recovery: {ErrorMessage}", Path, ex.Message);
        }
    }

    protected override Task OnStart()
        => ReRegisterConsumer();

    private async Task ReRegisterConsumer()
    {
        string existingConsumerTag = null;
        AsyncEventingBasicConsumer existingConsumer = null;

        lock (_consumerLock)
        {
            existingConsumerTag = _consumerTag;
            existingConsumer = _consumer;
        }

        // Cancel existing consumer if any (outside lock to avoid blocking)
        if (existingConsumerTag != null && _channel.Channel != null && _channel.Channel.IsOpen)
        {
            try
            {
                // Unsubscribe event handler before canceling
                if (existingConsumer != null)
                {
                    existingConsumer.ReceivedAsync -= OnMessageReceived;
                }

                await _channel.Channel.BasicCancelAsync(existingConsumerTag);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to cancel existing consumer tag {ConsumerTag} for queue {QueueName}", existingConsumerTag, Path);
            }
        }

        // Create and register new consumer (outside lock to avoid blocking)
        var newConsumer = new AsyncEventingBasicConsumer(_channel.Channel);
        newConsumer.ReceivedAsync += OnMessageReceived;

        var newConsumerTag = await _channel.Channel.BasicConsumeAsync(Path, autoAck: AcknowledgementMode == RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit, newConsumer);

        lock (_consumerLock)
        {
            _consumer = newConsumer;
            _consumerTag = newConsumerTag;
        }

        Logger.LogDebug("Consumer registered for queue {QueueName} with tag {ConsumerTag}", Path, newConsumerTag);
    }

    protected override async Task OnStop()
    {
        string consumerTagToCancel = null;
        AsyncEventingBasicConsumer consumerToCleanup = null;

        lock (_consumerLock)
        {
            consumerTagToCancel = _consumerTag;
            consumerToCleanup = _consumer;
            _consumerTag = null;
            _consumer = null;
        }

        // Unsubscribe event handler
        if (consumerToCleanup != null)
        {
            consumerToCleanup.ReceivedAsync -= OnMessageReceived;
        }

        // Cancel consumer (outside lock to avoid blocking)
        if (consumerTagToCancel != null && _channel.Channel != null && _channel.Channel.IsOpen)
        {
            try
            {
                await _channel.Channel.BasicCancelAsync(consumerTagToCancel);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to cancel consumer tag {ConsumerTag} for queue {QueueName} during stop", consumerTagToCancel, Path);
            }
        }
    }

    protected async Task OnMessageReceived(object sender, BasicDeliverEventArgs @event)
    {
        if (_consumer == null)
        {
            // In case during shutdown some outstanding message is delivered
            return;
        }

        Logger.LogDebug("Message arrived on queue {QueueName} from exchange {ExchangeName} with delivery tag {DeliveryTag}", Path, @event.Exchange, @event.DeliveryTag);
        Exception exception;
        try
        {
            var messageHeaders = new Dictionary<string, object>();

            if (@event.BasicProperties.Headers != null)
            {
                foreach (var header in @event.BasicProperties.Headers)
                {
                    messageHeaders.Add(header.Key, _headerValueConverter.ConvertFrom(header.Value));
                }
            }

            exception = await OnMessageReceived(messageHeaders, @event);
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        if (exception != null)
        {
            Logger.LogError(exception, "Error while processing message on queue {QueueName} from exchange {ExchangeName}: {ErrorMessage}", Path, @event.Exchange, exception.Message);
        }
    }

    protected abstract Task<Exception> OnMessageReceived(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage);

    public async Task NackMessage(BasicDeliverEventArgs @event, bool requeue)
    {
        // ToDo: Introduce a setting for allowing the client to allow for batching acks
        await _channel.Channel.BasicNackAsync(@event.DeliveryTag, multiple: false, requeue: requeue);
    }

    public async Task AckMessage(BasicDeliverEventArgs @event)
    {
        // ToDo: Introduce a setting for allowing the client to allow for batching acks
        await _channel.Channel.BasicAckAsync(@event.DeliveryTag, multiple: false);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        // Unsubscribe from channel recovery events
        if (_channel is RabbitMqChannelManager channelManager)
        {
            channelManager.ChannelRecovered -= OnChannelRecovered;
        }

        await base.DisposeAsyncCore();
    }
}
