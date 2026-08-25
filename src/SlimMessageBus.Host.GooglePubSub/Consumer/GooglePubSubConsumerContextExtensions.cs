namespace SlimMessageBus.Host.GooglePubSub;

public static class GooglePubSubConsumerContextExtensions
{
    private const string MessageKey = "GooglePubSub_Message";
    private const string SubscriptionNameKey = "GooglePubSub_SubscriptionName";

    public static PubsubMessage GetTransportMessage(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        return context.GetPropertyOrDefault<PubsubMessage>(MessageKey);
    }

    public static string GetSubscriptionName(this IConsumerContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        return context.GetPropertyOrDefault<string>(SubscriptionNameKey);
    }

    internal static void SetTransportMessage(this ConsumerContext context, PubsubMessage message)
        => context.Properties[MessageKey] = message;

    internal static void SetSubscriptionName(this ConsumerContext context, string subscriptionName)
        => context.Properties[SubscriptionNameKey] = subscriptionName;
}
