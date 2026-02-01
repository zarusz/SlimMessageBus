namespace SlimMessageBus.Host.RabbitMQ;

/// <summary>
/// Decorator for see <see cref="IMessageProcessor{TMessage}"/> that automatically acknowledges the message after processing.
/// </summary>
/// <param name="target"></param>
/// <param name="logger"></param>
/// <param name="acknowledgementMode"></param>
/// <param name="consumer"></param>
internal sealed class RabbitMqAutoAcknowledgeMessageProcessor : IMessageProcessor<BasicDeliverEventArgs>, IDisposable
{
    private readonly IMessageProcessor<BasicDeliverEventArgs> _target;
    private readonly ILogger _logger;
    private readonly RabbitMqMessageAcknowledgementMode _acknowledgementMode;
    private readonly IRabbitMqConsumer _consumer;

    public RabbitMqAutoAcknowledgeMessageProcessor(
        IMessageProcessor<BasicDeliverEventArgs> target,
        ILogger logger,
        RabbitMqMessageAcknowledgementMode acknowledgementMode,
        IRabbitMqConsumer consumer)
    {
        _target = target;
        _logger = logger;
        _acknowledgementMode = acknowledgementMode;
        _consumer = consumer;
    }

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _target.ConsumerSettings;

    public void Dispose()
    {
        if (_target is IDisposable targetDisposable)
        {
            targetDisposable.Dispose();
        }
    }

    public async Task<ProcessMessageResult> ProcessMessage(BasicDeliverEventArgs transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        var r = await _target.ProcessMessage(transportMessage, messageHeaders: messageHeaders, consumerContextProperties: consumerContextProperties, cancellationToken: cancellationToken);
        if (_acknowledgementMode == RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)
        {
            // Acknowledge after processing
            var confirmOption = r.Result switch
            {
                RabbitMqProcessResult.RequeueState => RabbitMqMessageConfirmOptions.Nack | RabbitMqMessageConfirmOptions.Requeue, // Re-queue after processing on transient failure
                ProcessResult.FailureState => RabbitMqMessageConfirmOptions.Nack,                                                            // Fail after processing failure (no re-queue)
                ProcessResult.SuccessState => RabbitMqMessageConfirmOptions.Ack,                                                             // Acknowledge after processing
                _ => throw new NotImplementedException()
            };

            await _consumer.ConfirmMessage(transportMessage, confirmOption, consumerContextProperties);
        }

        if (r.Exception != null)
        {
            // We rely on the IMessageProcessor to execute the ConsumerErrorHandler<T>, but if it's not registered in the DI, it fails, or there is another fatal error then the message will be lost.
            _logger.LogError(r.Exception, "Exchange {Exchange} - Queue {Queue}: Error processing message {Message}, delivery tag {DeliveryTag}", transportMessage.Exchange, _consumer.Path, transportMessage, transportMessage.DeliveryTag);
        }
        return r;
    }
}
