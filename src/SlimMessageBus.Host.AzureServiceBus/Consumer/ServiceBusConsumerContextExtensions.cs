namespace SlimMessageBus.Host.AzureServiceBus;

public static class ServiceBusConsumerContextExtensions
{
    private const string MessageKey = "ServiceBus_Message";
    private const string SubscriptionNameKey = "ServiceBus_SubscriptionName";

    public static ServiceBusReceivedMessage GetTransportMessage(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return context.GetPropertyOrDefault<ServiceBusReceivedMessage>(MessageKey);
    }

    internal static void SetTransportMessage(this ConsumerContext context, ServiceBusReceivedMessage message)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Properties[MessageKey] = message;
    }

    public static string GetSubscriptionName(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        return context.GetPropertyOrDefault<string>(SubscriptionNameKey);
    }

    internal static void SetSubscriptionName(this ConsumerContext context, string subscriptionName)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        context.Properties[SubscriptionNameKey] = subscriptionName;
    }
}
