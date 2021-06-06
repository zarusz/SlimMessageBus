namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    using Microsoft.Azure.ServiceBus;
    using SlimMessageBus.Host.Config;

    internal static class MessageExtensions
    {
        public static string FormatIf(this AbstractConsumerSettings consumerSettings, Message msg, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            if (consumerSettings.PathKind == PathKind.Queue)
            {
                return $"Queue: {consumerSettings.Path}, SequenceNumber: {msg.SystemProperties.SequenceNumber}, DeliveryCount: {msg.SystemProperties.DeliveryCount}";
            }

            return $"Topic: {consumerSettings.Path}, SubscriptionName: {consumerSettings.GetSubscriptionName()}, SequenceNumber: {msg.SystemProperties.SequenceNumber}, DeliveryCount: {msg.SystemProperties.DeliveryCount}";
        }

        public static string FormatIf(this ConsumerSettings consumerSettings, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            if (consumerSettings.PathKind == PathKind.Queue)
            {
                return $"Queue: {consumerSettings.Path}, MessageType: {consumerSettings.MessageType}";
            }

            return $"Topic: {consumerSettings.Path}, SubscriptionName: {consumerSettings.GetSubscriptionName()}, MessageType: {consumerSettings.MessageType}";
        }

        public static string FormatIf(this AbstractConsumerSettings settings, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            if (settings.PathKind == PathKind.Queue)
            {
                return $"Queue: {settings.Path}";
            }
            return $"Topic: {settings.Path}, SubscriptionName: {settings.GetSubscriptionName()}";
        }

    }
}
