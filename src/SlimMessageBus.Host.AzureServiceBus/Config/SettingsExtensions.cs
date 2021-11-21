namespace SlimMessageBus.Host.AzureServiceBus
{
    using SlimMessageBus.Host.Config;
    using System;

    public static class SettingsExtensions
    {
        private const string SubscriptionNameKey = "SubscriptionName";

        internal static void SetSubscriptionName(this AbstractConsumerSettings consumerSettings, string subscriptionName)
        {
            if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

            consumerSettings.Properties[SubscriptionNameKey] = subscriptionName;
        }

        internal static string GetSubscriptionName(this AbstractConsumerSettings consumerSettings, bool required = true)
        {
            if (!consumerSettings.Properties.ContainsKey(SubscriptionNameKey) && !required)
            {
                return null;
            }
            return consumerSettings.Properties[SubscriptionNameKey] as string;
        }

        private static void AssertIsTopicForSubscriptionName(AbstractConsumerSettings settings)
        {
            if (settings is null) throw new ArgumentNullException(nameof(settings));

            if (settings.PathKind == PathKind.Queue)
            {
                var methodName = $".{nameof(SubscriptionName)}(...)";

                var messageType = settings is ConsumerSettings consumerSettings
                    ? consumerSettings.MessageType.FullName
                    : string.Empty;

                throw new ConfigurationMessageBusException($"The subscription name configuration ({methodName}) does not apply to Azure ServiceBus queues (it only applies to topic consumers). Remove the {methodName} configuration for type {messageType} and queue {settings.Path} or change the consumer configuration to consume from topic {settings.Path} instead.");
            }
        }

        /// <summary>
        /// Configures the subscription name when consuming form Azure ServiceBus topic.
        /// Not applicable when consuming from Azure ServiceBus queue.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="subscriptionName"></param>
        /// <returns></returns>
        public static T SubscriptionName<T>(this T builder, string subscriptionName)
            where T : AbstractConsumerBuilder
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

            AssertIsTopicForSubscriptionName(builder.ConsumerSettings);

            builder.ConsumerSettings.SetSubscriptionName(subscriptionName);
            return builder;
        }

        /// <summary>
        /// Configures the subscription name when consuming form Azure ServiceBus topic.
        /// Not applicable when consuming from Azure ServiceBus queue.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="subscriptionName"></param>
        /// <returns></returns>
        public static RequestResponseBuilder SubscriptionName(this RequestResponseBuilder builder, string subscriptionName)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (subscriptionName is null) throw new ArgumentNullException(nameof(subscriptionName));

            AssertIsTopicForSubscriptionName(builder.Settings);

            builder.Settings.SetSubscriptionName(subscriptionName);
            return builder;
        }
    }
}
