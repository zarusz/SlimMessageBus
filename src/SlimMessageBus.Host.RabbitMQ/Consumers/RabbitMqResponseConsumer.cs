namespace SlimMessageBus.Host.RabbitMQ;

public class RabbitMqResponseConsumer : AbstractRabbitMqConsumer
{
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;

    public RabbitMqResponseConsumer(ILoggerFactory loggerFactory, IRabbitMqChannel channel, string queueName, RequestResponseSettings requestResponseSettings, MessageBusBase messageBus, IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>(), channel, queueName, headerValueConverter)
    {
        _messageProcessor = new ResponseMessageProcessor<BasicDeliverEventArgs>(loggerFactory, requestResponseSettings, messageBus, m => m.Body.ToArray());
    }

    protected override async Task<Exception> OnMessageRecieved(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
    {
        var (exception, _, _, _) = await _messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, cancellationToken: CancellationToken);
        if (exception == null)
        {
            AckMessage(transportMessage);
        }
        else
        {
            NackMessage(transportMessage, requeue: false);
        }
        return exception;
    }
}
