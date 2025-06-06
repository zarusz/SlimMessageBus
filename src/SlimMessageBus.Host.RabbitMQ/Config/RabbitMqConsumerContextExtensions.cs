﻿namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client.Events;

public static class RabbitMqConsumerContextExtensions
{
    private static readonly string Key = "RabbitMq_MessageConfirmAction";

    public static BasicDeliverEventArgs GetTransportMessage(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return context.GetPropertyOrDefault<BasicDeliverEventArgs>(RabbitMqProperties.Message.Key);
    }

    static internal void SetTransportMessage(this IConsumerContext context, BasicDeliverEventArgs message)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Properties[RabbitMqProperties.Message.Key] = message;
    }

    static internal void SetConfirmAction(this IConsumerContext consumerContext, RabbitMqMessageConfirmAction messageConfirmAction)
        => consumerContext.Properties[Key] = messageConfirmAction;

    static internal void ConfirmAction(this IConsumerContext consumerContext, RabbitMqMessageConfirmOptions option)
    {
        var messageConfirmAction = consumerContext.GetPropertyOrDefault<RabbitMqMessageConfirmAction>(Key)
            ?? throw new ConsumerMessageBusException("Cannot perform RabbitMq message confirmation at this point");

        messageConfirmAction(option);
    }

    /// <summary>
    /// Sends an Ack confirm for the processed message.
    /// </summary>
    /// <param name="consumerContext"></param>
    public static void Ack(this IConsumerContext consumerContext)
        => ConfirmAction(consumerContext, RabbitMqMessageConfirmOptions.Ack);

    /// <summary>
    /// Sends an Nack (negative ack) for the processed message with the setting to NOT redeliver it.
    /// </summary>
    /// <param name="consumerContext"></param>
    public static void Nack(this IConsumerContext consumerContext)
        => ConfirmAction(consumerContext, RabbitMqMessageConfirmOptions.Nack);

    /// <summary>
    /// Sends an Nack (negative ack) for the processed message with the setting to requeue it.
    /// </summary>
    /// <param name="consumerContext"></param>
    public static void NackWithRequeue(this IConsumerContext consumerContext)
        => ConfirmAction(consumerContext, RabbitMqMessageConfirmOptions.Nack | RabbitMqMessageConfirmOptions.Requeue);
}
