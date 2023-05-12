namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

using SlimMessageBus.Host.Serialization;

public class RabbitMqConsumer : AbstractConsumer
{
    private readonly IRabbitMqChannel _channel;
    private readonly string _queueName;
    private readonly MessageBusBase _messageBus;
    private readonly IMessageSerializer _headerSerializer;
    private readonly ConsumerInstanceMessageProcessor<BasicDeliverEventArgs> _messageProcessor;
    private AsyncEventingBasicConsumer _consumer;
    private string _consumerTag;

    public RabbitMqConsumer(ILoggerFactory loggerFactory, IRabbitMqChannel channel, string queueName, IList<ConsumerSettings> consumers, IMessageSerializer serializer, MessageBusBase messageBus, IMessageSerializer headerSerializer)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>())
    {
        _channel = channel;
        _queueName = queueName;
        _messageBus = messageBus;
        _headerSerializer = headerSerializer;
        _messageProcessor = new ConsumerInstanceMessageProcessor<BasicDeliverEventArgs>(
            consumers,
            messageBus,
            path: queueName,
            messageProvider: (messageType, m) => serializer.Deserialize(messageType, m.Body.ToArray()),
            consumerContextInitializer: InitializeConsumerContext);
    }

    private static void InitializeConsumerContext(BasicDeliverEventArgs m, ConsumerContext consumerContext) => consumerContext.SetTransportMessage(m);

    protected async Task OnMessageRecieved(object sender, BasicDeliverEventArgs @event)
    {
        if (_consumer == null)
        {
            // In case during shutdown some outstanding message is delivered
            return;
        }

        Logger.LogDebug("Message arrived on queue {QueueName} from exchange {ExchangeName} with delivery tag {DeliveryTag}", _queueName, @event.Exchange, @event.DeliveryTag);
        Exception exception;
        try
        {
            var messageHeaders = new Dictionary<string, object>();

            if (@event.BasicProperties.Headers != null)
            {
                foreach (var header in @event.BasicProperties.Headers)
                {
                    var value = header.Value is byte[] bytes
                        ? _headerSerializer.Deserialize(typeof(object), bytes)
                        : header.Value;

                    messageHeaders.Add(header.Key, value);
                }
            }

            (exception, _, _, var message) = await _messageProcessor.ProcessMessage(@event, messageHeaders: messageHeaders, cancellationToken: CancellationToken);

            if (exception == null)
            {
                AckMessage(@event);
            }
            else
            {
                await OnMessageError(@event, messageHeaders, message, exception);
            }
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        if (exception != null)
        {
            Logger.LogError(exception, "Error while processing message on queue {QueueName} from exchange {ExchangeName}: {ErrorMessage}", _queueName, @event.Exchange, exception.Message);
        }
    }

    private async Task OnMessageError(BasicDeliverEventArgs @event, Dictionary<string, object> messageHeaders, object message, Exception exception)
    {
        var consumerContext = CreateConsumerContext(@event, messageHeaders);

        // Obtain from cache closed generic type IConsumerErrorHandler<TMessage> of the message type
        var consumerErrorHandlerType = _messageBus.RuntimeTypeCache.GetClosedGenericType(typeof(RabbitMqConsumerErrorHandler<>), message.GetType());
        // Resolve IConsumerErrorHandler<> of the message type
        var consumerErrorHandler = _messageBus.Settings.ServiceProvider.GetService(consumerErrorHandlerType) as IConsumerErrorHandlerUntyped;
        if (consumerErrorHandler != null)
        {
            var handled = await consumerErrorHandler.OnHandleError(message, consumerContext, exception);
            if (handled)
            {
                Logger.LogDebug("Error handling was performed by {ConsumerErrorHandlerType}", consumerErrorHandler.GetType().Name);
                return;
            }
        }

        // By default nack the message
        NackMessage(@event, requeue: false);
    }

    private ConsumerContext CreateConsumerContext(BasicDeliverEventArgs @event, Dictionary<string, object> messageHeaders)
    {
        var consumerContext = new ConsumerContext
        {
            Path = _queueName,
            CancellationToken = CancellationToken,
            Bus = _messageBus,
            Headers = messageHeaders,
            ConsumerInvoker = null,
            Consumer = null,
        };

        consumerContext.SetConfirmAction(option =>
        {
            if ((option & RabbitMqMessageConfirmOption.Ack) != 0)
            {
                AckMessage(@event);
            }
            else if ((option & RabbitMqMessageConfirmOption.Nack) != 0)
            {
                NackMessage(@event, requeue: (option & RabbitMqMessageConfirmOption.Requeue) != 0);
            }
        });

        InitializeConsumerContext(@event, consumerContext);

        return consumerContext;
    }

    private void NackMessage(BasicDeliverEventArgs @event, bool requeue)
    {
        lock (_channel.ChannelLock)
        {
            // ToDo: Introduce a setting for allowing the client to allow for batching acks
            _channel.Channel.BasicNack(@event.DeliveryTag, multiple: false, requeue: requeue);
        }
    }

    private void AckMessage(BasicDeliverEventArgs @event)
    {
        lock (_channel.ChannelLock)
        {
            // ToDo: Introduce a setting for allowing the client to allow for batching acks
            _channel.Channel.BasicAck(@event.DeliveryTag, multiple: false);
        }
    }

    protected override Task OnStart()
    {
        _consumer = new AsyncEventingBasicConsumer(_channel.Channel);
        _consumer.Received += OnMessageRecieved;

        lock (_channel.ChannelLock)
        {
            _consumerTag = _channel.Channel.BasicConsume(_queueName, autoAck: false, _consumer);
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
}
