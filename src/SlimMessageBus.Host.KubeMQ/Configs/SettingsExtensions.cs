namespace SlimMessageBus.Host.KubeMQ.Configs
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class SettingsExtensions
    {
        private const string GroupKey = "Group";

        public static void SetGroup(this AbstractConsumerSettings consumerSettings, string group)
        {
            if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

            consumerSettings.Properties[GroupKey] = group;
        }

        public static string GetGroup(this AbstractConsumerSettings consumerSettings)
        {
            if (consumerSettings == null) throw new ArgumentNullException(nameof(consumerSettings));

            if (!consumerSettings.Properties.TryGetValue(GroupKey, out var group))
            {
                return null;
            }
            return group as string;
        }
    }
}
