namespace SlimMessageBus.Host.RabbitMQ;

using System.Collections.Generic;

public class RabbitMqResponseConsumer : AbstractRabbitMqConsumer
{
    private readonly IMessageProcessor<BasicDeliverEventArgs> _messageProcessor;

    protected override RabbitMqMessageAcknowledgementMode AcknowledgementMode => RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade;

    public RabbitMqResponseConsumer(
        ILoggerFactory loggerFactory,
        IEnumerable<IAbstractConsumerInterceptor> interceptors,
        IRabbitMqChannel channel,
        string queueName,
        RequestResponseSettings requestResponseSettings,
        MessageProvider<BasicDeliverEventArgs> messageProvider,
        IPendingRequestStore pendingRequestStore,
        ICurrentTimeProvider currentTimeProvider,
        IHeaderValueConverter headerValueConverter)

        : base(loggerFactory.CreateLogger<RabbitMqResponseConsumer>(),
               [requestResponseSettings],
               interceptors,
               channel,
               queueName,
               headerValueConverter)
    {
        _messageProcessor = new ResponseMessageProcessor<BasicDeliverEventArgs>(loggerFactory, requestResponseSettings, messageProvider, pendingRequestStore, currentTimeProvider);
    }

    protected override async Task<Exception> OnMessageReceived(Dictionary<string, object> messageHeaders, BasicDeliverEventArgs transportMessage)
    {
        var r = await _messageProcessor.ProcessMessage(transportMessage, messageHeaders: messageHeaders, cancellationToken: CancellationToken);
        switch (r.Result)
        {
            case RabbitMqProcessResult.RequeueState:
                NackMessage(transportMessage, requeue: true);
                break;

            case ProcessResult.FailureState:
                NackMessage(transportMessage, requeue: false);
                break;

            case ProcessResult.SuccessState:
                AckMessage(transportMessage);
                break;

            default:
                throw new NotImplementedException();
        }

        return r.Exception;
    }
}
