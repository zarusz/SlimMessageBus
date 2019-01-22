using SlimMessageBus.Host.AzureServiceBus.Config;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    public static class SettingsExtensions
    {
        public static void SetSubscriptionName(this AbstractConsumerSettings consumerSettings, string subscriptionName)
        {
            consumerSettings.Properties["SubscriptionName"] = subscriptionName;
        }

        public static string GetSubscriptionName(this AbstractConsumerSettings consumerSettings)
        {
            return consumerSettings.Properties["SubscriptionName"] as string;
        }

        private static void AssertIsTopicForSubscriptionName(AbstractConsumerSettings settings)
        {
            if (settings.GetKind() == PathKind.Queue)
            {
                var methodName = $".{nameof(SubscriptionName)}(...)";

                var messageType = settings is ConsumerSettings consumerSettings
                    ? consumerSettings.MessageType.FullName
                    : string.Empty;
                throw new ConfigurationMessageBusException($"The subscription name configuration ({methodName}) does not apply to Azure ServiceBus queues (it only applies to topic consumers). Remove the {methodName} configuration for type {messageType} and queue {settings.Topic} or change the consumer configuration to consume from topic {settings.Topic} instead.");
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

        /// <summary>
        /// Configures the subscription name when consuming form Azure ServiceBus topic.
        /// Not applicable when consuming from Azure ServiceBus queue.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="subscriptionName"></param>
        /// <returns></returns>
        public static RequestResponseBuilder SubscriptionName(this RequestResponseBuilder builder, string subscriptionName)
        {
            AssertIsTopicForSubscriptionName(builder.Settings);

            builder.Settings.SetSubscriptionName(subscriptionName);
            return builder;
        }
    }
}
