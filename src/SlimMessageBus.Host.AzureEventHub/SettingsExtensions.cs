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

        public static T Group<T>(this T builder, string group)
            where T : AbstractTopicConsumerBuilder
        {
            builder.ConsumerSettings.SetGroup(group);
            return builder;
        }

        public static RequestResponseBuilder Group(this RequestResponseBuilder builder, string group)
        {
            builder.Settings.SetGroup(group);
            return builder;
        }
    }
}
