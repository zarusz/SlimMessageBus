namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

public abstract class AbstractRabbitMqConsumer : AbstractConsumer
{
    private readonly IRabbitMqChannel _channel;
    private readonly IHeaderValueConverter _headerValueConverter;
    private AsyncEventingBasicConsumer _consumer;
    private string _consumerTag;

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
    }

    protected override Task OnStart()
    {
        _consumer = new AsyncEventingBasicConsumer(_channel.Channel);
        _consumer.Received += OnMessageReceived;

        lock (_channel.ChannelLock)
        {
            _consumerTag = _channel.Channel.BasicConsume(Path, autoAck: AcknowledgementMode == RabbitMqMessageAcknowledgementMode.AckAutomaticByRabbit, _consumer);
        }

        return Task.CompletedTask;
    }

    protected override Task OnStop()
    {
        lock (_channel.ChannelLock)
        {
            _channel.Channel.BasicCancel(_consumerTag);
        }
        _consumerTag = null;
        _consumer = null;

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
}
