namespace SlimMessageBus.Host.AzureEventHub;

internal static class SettingsExtensions
{
    private const string GroupKey = "Eh_Group";
    private const string KeyProviderKey = "Eh_KeyProvider";

    public static void SetGroup(this AbstractConsumerSettings consumerSettings, string group)
        => consumerSettings.Properties[GroupKey] = group;

    public static string GetGroup(this AbstractConsumerSettings consumerSettings)
        => consumerSettings.Properties[GroupKey] as string;

    public static void SetKeyProvider(this ProducerSettings producerSettings, Func<object, string> keyProvider)
        => producerSettings.Properties[KeyProviderKey] = keyProvider;

    public static Func<object, string> GetKeyProvider(this ProducerSettings producerSettings)
        => producerSettings.Properties.ContainsKey(KeyProviderKey) ? producerSettings.Properties[KeyProviderKey] as Func<object, string> : null;
}
