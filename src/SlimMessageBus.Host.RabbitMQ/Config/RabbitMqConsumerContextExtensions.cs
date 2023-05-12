namespace SlimMessageBus.Host.RabbitMQ;

using global::RabbitMQ.Client.Events;

public static class RabbitMqConsumerContextExtensions
{
    public static BasicDeliverEventArgs GetTransportMessage(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        
        return context.GetPropertyOrDefault<BasicDeliverEventArgs>(RabbitMqProperties.Message);
    }

    internal static void SetTransportMessage(this ConsumerContext context, BasicDeliverEventArgs message)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Properties[RabbitMqProperties.Message] = message;
    }
}
