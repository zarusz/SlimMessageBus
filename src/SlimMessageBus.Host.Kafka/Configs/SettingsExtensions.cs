using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Kafka.Configs
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

        /// <summary>
        /// Configures the Kafka consumer group.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static T Group<T>(this T builder, string group)
            where T : AbstractTopicConsumerBuilder
        {
            builder.ConsumerSettings.SetGroup(group);
            return builder;
        }

        /// <summary>
        /// Configures the Kafka consumer group.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static RequestResponseBuilder Group(this RequestResponseBuilder builder, string group)
        {
            builder.Settings.SetGroup(group);
            return builder;
        }
    }
}
