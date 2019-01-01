using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public static class SettingsExtensions
    {
        public static void SetSubscriptionName(this ConsumerSettings consumerSettings, string group)
        {
            consumerSettings.Properties["SubscriptionName"] = group;
        }

        public static string GetSubscriptionName(this ConsumerSettings consumerSettings)
        {
            return consumerSettings.Properties["SubscriptionName"] as string;
        }

        public static void SetSubscriptionName(this RequestResponseSettings consumerSettings, string group)
        {
            consumerSettings.Properties["SubscriptionName"] = group;
        }

        public static string GetSubscriptionName(this RequestResponseSettings consumerSettings)
        {
            return consumerSettings.Properties["SubscriptionName"] as string;
        }

        public static TopicSubscriberBuilder<TMessage> SubscriptionName<TMessage>(this TopicSubscriberBuilder<TMessage> builder, string subscriptionName)
        {
            builder.ConsumerSettings.SetSubscriptionName(subscriptionName);
            return builder;
        }

        public static TopicHandlerBuilder<TRequest, TResponse> SubscriptionName<TRequest, TResponse>(this TopicHandlerBuilder<TRequest, TResponse> builder, string subscriptionName)
            where TRequest : IRequestMessage<TResponse>
        {
            builder.ConsumerSettings.SetSubscriptionName(subscriptionName);
            return builder;
        }

        public static RequestResponseBuilder SubscriptionName(this RequestResponseBuilder builder, string subscriptionName)
        {
            builder.Settings.SetSubscriptionName(subscriptionName);
            return builder;
        }
    }
}
