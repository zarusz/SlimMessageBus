namespace SlimMessageBus.Host.RabbitMQ;

public class RabbitMqResponseConsumer : AbstractRabbitMqConsumer
{
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;

    protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade;

    public RabbitMqResponseConsumer(ILoggerFactory loggerFactory, IRabbitMqChannel channel, string queueName, RequestResponseSettings requestResponseSettings, MessageBusBase messageBus, IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqConsumer>(), channel, queueName, headerValueConverter)
    {
        _messageProcessor = new ResponseMessageProcessor<BasicDeliverEventArgs>(loggerFactory, requestResponseSettings, messageBus, m => m.Body.ToArray());
    }

    protected override async Task<Exception> OnMessageRecieved(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
    {
        var r = await _messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, cancellationToken: CancellationToken);
        if (r.Exception == null)
        {
            AckMessage(transportMessage);
        }
        else
        {
            NackMessage(transportMessage, requeue: false);
        }
        return r.Exception;
    }
}
