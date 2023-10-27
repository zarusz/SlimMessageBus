namespace SlimMessageBus.Host.RabbitMQ;

public static class RabbitMqConsumerContextExtensionsForErrorHandler
{
    private static readonly string Key = "RabbitMq_MessageConfirmAction";

    static internal void SetConfirmAction(this IConsumerContext consumerContext, Action<RabbitMqMessageConfirmOption> messageConfirmAction)
        => consumerContext.Properties[Key] = messageConfirmAction;

    static internal void ConfirmAction(this IConsumerContext consumerContext, RabbitMqMessageConfirmOption option)
    {
        var messageConfirmAction = consumerContext.Properties[Key] as Action<RabbitMqMessageConfirmOption>;
        messageConfirmAction(option);
    }

    /// <summary>
    /// Sends and Ack message for the failed message.
    /// </summary>
    /// <param name="consumerContext"></param>
    public static void Ack(this IConsumerContext consumerContext)
        => ConfirmAction(consumerContext, RabbitMqMessageConfirmOption.Ack);

    /// <summary>
    /// Sends and NAck message with the setting to NOT redeliver it.
    /// </summary>
    /// <param name="consumerContext"></param>
    public static void Nack(this IConsumerContext consumerContext)
        => ConfirmAction(consumerContext, RabbitMqMessageConfirmOption.Nack);

    /// <summary>
    /// Sends and NAck message with the setting to requeue it.
    /// </summary>
    /// <param name="consumerContext"></param>
    public static void NackWithRequeue(this IConsumerContext consumerContext)
        => ConfirmAction(consumerContext, RabbitMqMessageConfirmOption.Nack | RabbitMqMessageConfirmOption.Requeue);
}
