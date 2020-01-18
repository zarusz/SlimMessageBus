using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public static class SettingsExtensions
    {
        private const string GroupKey = "Group";

        public static void SetGroup(this AbstractConsumerSettings consumerSettings, string group)
        {
            consumerSettings.Properties[GroupKey] = group;
        }

        public static string GetGroup(this AbstractConsumerSettings consumerSettings)
        {
            return consumerSettings.Properties[GroupKey] as string;
        }
    }
}
