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

    private Task ReRegisterConsumer()
    {
        lock (_consumerLock)
        {
            // Cancel existing consumer if any
            if (_consumerTag != null && _channel.Channel != null && _channel.Channel.IsOpen)
            {
                try
                {
                    lock (_channel.ChannelLock)
                    {
                        _channel.Channel.BasicCancel(_consumerTag);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to cancel existing consumer tag {ConsumerTag} for queue {QueueName}", _consumerTag, Path);
                }
            }

            // Create new consumer
            _consumer = new AsyncEventingBasicConsumer(_channel.Channel);
            _consumer.Received += OnMessageReceived;

            lock (_channel.ChannelLock)
            {
                _consumerTag = _channel.Channel.BasicConsume(Path, autoAck: AcknowledgementMode == RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit, _consumer);
            }

            Logger.LogDebug("Consumer registered for queue {QueueName} with tag {ConsumerTag}", Path, _consumerTag);
        }

        return Task.CompletedTask;
    }

    protected override Task OnStop()
    {
        lock (_consumerLock)
        {
            if (_consumerTag != null && _channel.Channel != null && _channel.Channel.IsOpen)
            {
                try
                {
                    lock (_channel.ChannelLock)
                    {
                        _channel.Channel.BasicCancel(_consumerTag);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to cancel consumer tag {ConsumerTag} for queue {QueueName} during stop", _consumerTag, Path);
                }
            }
            _consumerTag = null;

            if (_consumer != null)
            {
                _consumer.Received -= OnMessageReceived;
                _consumer = null;
            }
        }

        return Task.CompletedTask;
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

    public void NackMessage(BasicDeliverEventArgs @event, bool requeue)
    {
        lock (_channel.ChannelLock)
        {
            // ToDo: Introduce a setting for allowing the client to allow for batching acks
            _channel.Channel.BasicNack(@event.DeliveryTag, multiple: false, requeue: requeue);
        }
    }

    public void AckMessage(BasicDeliverEventArgs @event)
    {
        lock (_channel.ChannelLock)
        {
            // ToDo: Introduce a setting for allowing the client to allow for batching acks
            _channel.Channel.BasicAck(@event.DeliveryTag, multiple: false);
        }
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
