namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus;

public static class ServiceBusConsumerContextExtensions
{
    private const string MessageKey = "ServiceBus_Message";

    public static ServiceBusReceivedMessage GetTransportMessage(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return context.GetPropertyOrDefault<ServiceBusReceivedMessage>(MessageKey);
    }

    public static void SetTransportMessage(this ConsumerContext context, ServiceBusReceivedMessage message)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Properties[MessageKey] = message;
    }
}
