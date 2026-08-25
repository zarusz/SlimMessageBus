namespace SlimMessageBus.Host.GooglePubSub;

internal static class GooglePubSubSettingsExtensions
{
    internal static string GetSubscriptionName(this AbstractConsumerSettings settings)
        => settings.GetOrDefault(GooglePubSubProperties.SubscriptionName);

    internal static void SetSubscriptionName(this HasProviderExtensions settings, string subscriptionName)
        => GooglePubSubProperties.SubscriptionName.Set(settings, subscriptionName);
}
