using SlimMessageBus.Host.AzureServiceBus.Config;
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

        private static void AssertIsTopicForSubscriptionName(ConsumerSettings consumerSettings)
        {
            if (consumerSettings.GetKind() == PathKind.Queue)
            {
                var methodName = $".{nameof(SubscriptionName)}(...)";
                throw new ConfigurationMessageBusException($"The subscription name configuration ({methodName}) does not apply to Azure ServiceBus queues (it only applies to topic consumers). Remove the {methodName} configuration for type {consumerSettings.MessageType} and queue {consumerSettings.Topic} or change the consumer configuration to consume from topic {consumerSettings.Topic} instead.");
            }
        }

        /// <summary>
        /// Configures the subscription name when consuming form Azure ServiceBus topic.
        /// Not applicable when consuming from Azure ServiceBus queue.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="builder"></param>
        /// <param name="subscriptionName"></param>
        /// <returns></returns>
        public static TopicSubscriberBuilder<TMessage> SubscriptionName<TMessage>(this TopicSubscriberBuilder<TMessage> builder, string subscriptionName)
        {
            AssertIsTopicForSubscriptionName(builder.ConsumerSettings);

            builder.ConsumerSettings.SetSubscriptionName(subscriptionName);
            return builder;
        }

        /// <summary>
        /// Configures the subscription name when consuming form Azure ServiceBus topic.
        /// Not applicable when consuming from Azure ServiceBus queue.
        /// </summary>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="builder"></param>
        /// <param name="subscriptionName"></param>
        /// <returns></returns>
        public static TopicHandlerBuilder<TRequest, TResponse> SubscriptionName<TRequest, TResponse>(this TopicHandlerBuilder<TRequest, TResponse> builder, string subscriptionName)
            where TRequest : IRequestMessage<TResponse>
        {
            AssertIsTopicForSubscriptionName(builder.ConsumerSettings);

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
