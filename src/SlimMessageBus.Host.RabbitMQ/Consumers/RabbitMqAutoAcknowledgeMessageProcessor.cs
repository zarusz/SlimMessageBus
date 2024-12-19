namespace SlimMessageBus.Host.RabbitMQ;

/// <summary>
/// Decorator for see <see cref="IMessageProcessor{TMessage}"/> that automatically acknowledges the message after processing.
/// </summary>
/// <param name="target"></param>
/// <param name="logger"></param>
/// <param name="acknowledgementMode"></param>
/// <param name="consumer"></param>
internal sealed class RabbitMqAutoAcknowledgeMessageProcessor(IMessageProcessor<BasicDeliverEventArgs> target,
                                                       ILogger logger,
                                                       RabbitMqMessageAcknowledgementMode acknowledgementMode,
                                                       IRabbitMqConsumer consumer)
    : IMessageProcessor<BasicDeliverEventArgs>, IDisposable
{
    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => target.ConsumerSettings;

    public void Dispose()
    {
        if (target is IDisposable targetDisposable)
        {
            targetDisposable.Dispose();
        }
    }

    public async Task<ProcessMessageResult> ProcessMessage(BasicDeliverEventArgs transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        var r = await target.ProcessMessage(transportMessage, messageHeaders: messageHeaders, consumerContextProperties: consumerContextProperties, cancellationToken: cancellationToken);

        if (acknowledgementMode == RabbitMqMessageAcknowledgementMode.ConfirmAfterMessageProcessingWhenNoManualConfirmMade)
        {
            // Acknowledge after processing
            var confirmOption = r.Exception != null
                ? RabbitMqMessageConfirmOptions.Nack // NAck after processing when message fails (unless the user already acknowledged in any way).
                : RabbitMqMessageConfirmOptions.Ack; // Acknowledge after processing

            consumer.ConfirmMessage(transportMessage, confirmOption, consumerContextProperties);
        }

        if (r.Exception != null)
        {
            // We rely on the IMessageProcessor to execute the ConsumerErrorHandler<T>, but if it's not registered in the DI, it fails, or there is another fatal error then the message will be lost.
            logger.LogError(r.Exception, "Exchange {Exchange} - Queue {Queue}: Error processing message {Message}, delivery tag {DeliveryTag}", transportMessage.Exchange, consumer.QueueName, transportMessage, transportMessage.DeliveryTag);
        }
        return r;
    }
}
