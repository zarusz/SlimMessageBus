using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    internal static class HasProviderExtensionsExtensions
    {
        internal static HasProviderExtensions SetKind(this HasProviderExtensions producerSettings, PathKind kind)
        {
            producerSettings.Properties["kind"] = kind;
            return producerSettings;
        }

        internal static PathKind GetKind(this HasProviderExtensions producerSettings)
        {
            if (producerSettings.Properties.TryGetValue("kind", out var kind))
            {
                return (PathKind) kind;
            }
            return PathKind.Topic;
        }
    }
}