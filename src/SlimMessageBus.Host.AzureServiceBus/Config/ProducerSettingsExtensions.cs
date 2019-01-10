using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Config
{
    internal static class ProducerSettingsExtensions
    {
        internal static ProducerSettings SetKind(this ProducerSettings producerSettings, PathKind kind)
        {
            producerSettings.Properties["kind"] = kind;
            return producerSettings;
        }

        internal static PathKind GetKind(this ProducerSettings producerSettings)
        {
            if (producerSettings.Properties.TryGetValue("kind", out var kind))
            {
                return (PathKind) kind;
            }
            return PathKind.Topic;
        }
    }
}