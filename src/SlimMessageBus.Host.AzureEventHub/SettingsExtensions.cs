using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    public static class SettingsExtensions
    {
        public static void SetGroup(this ConsumerSettings consumerSettings, string group)
        {
            consumerSettings.Properties["Group"] = group;
        }

        public static string GetGroup(this ConsumerSettings consumerSettings)
        {
            return consumerSettings.Properties["Group"] as string;
        }

        public static void SetGroup(this RequestResponseSettings consumerSettings, string group)
        {
            consumerSettings.Properties["Group"] = group;
        }

        public static string GetGroup(this RequestResponseSettings consumerSettings)
        {
            return consumerSettings.Properties["Group"] as string;
        }

        public static TopicSubscriberBuilder<TMessage> Group<TMessage>(this TopicSubscriberBuilder<TMessage> builder, string group)
        {
            builder.ConsumerSettings.SetGroup(group);
            return builder;
        }

        public static TopicHandlerBuilder<TRequest, TResponse> Group<TRequest, TResponse>(this TopicHandlerBuilder<TRequest, TResponse> builder, string group)
            where TRequest : IRequestMessage<TResponse>
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
