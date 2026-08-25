namespace SlimMessageBus.Host.GooglePubSub;

internal static class GooglePubSubProperties
{
    internal static readonly ProviderExtensionProperty<string> SubscriptionName = new("GooglePubSub_SubscriptionName");
    internal static readonly ProviderExtensionProperty<GooglePubSubMessageModifier<object>> MessageModifier = new("GooglePubSub_MessageModifier");
    internal static readonly ProviderExtensionProperty<Action<Topic>> CreateTopicOptions = new("GooglePubSub_CreateTopicOptions");
    internal static readonly ProviderExtensionProperty<Action<Subscription>> CreateSubscriptionOptions = new("GooglePubSub_CreateSubscriptionOptions");
}
