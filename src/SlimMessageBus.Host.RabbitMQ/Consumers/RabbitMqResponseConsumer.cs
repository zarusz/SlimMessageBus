namespace SlimMessageBus.Host.RabbitMQ;

public class RabbitMqResponseConsumer : AbstractRabbitMqConsumer
{
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;
    private readonly bool _requeueOnFailure;

    protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade;

    public RabbitMqResponseConsumer(
        ILoggerFactory loggerFactory,
        IRabbitMqChannel channel,
        string queueName,
        RequestResponseSettings requestResponseSettings,
        MessageProvider<BasicDeliverEventArgs> messageProvider,
        IPendingRequestStore pendingRequestStore,
        ICurrentTimeProvider currentTimeProvider,
        IHeaderValueConverter headerValueConverter)
        : base(loggerFactory.CreateLogger<RabbitMqResponseConsumer>(), channel, queueName, headerValueConverter)
    {
        _messageProcessor = new ResponseMessageProcessor<BasicDeliverEventArgs>(loggerFactory, requestResponseSettings, messageProvider, pendingRequestStore, currentTimeProvider);
        _requeueOnFailure = requestResponseSettings.GetOrDefault(RabbitMqProperties.ReqeueOnFailure, false);
    }

    protected override async Task<Exception> OnMessageReceived(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
    {
        var r = await _messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, cancellationToken: CancellationToken);
        switch (r.Result)
        {
            case ProcessResult.Abandon:
                NackMessage(transportMessage, requeue: false);
                break;

            case ProcessResult.Fail:
                NackMessage(transportMessage, requeue: _requeueOnFailure);
                break;

            case ProcessResult.Success:
                AckMessage(transportMessage);
                break;

            default:
                throw new NotImplementedException();
        }

        return r.Exception;
    }
}
