using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    internal static class MessageExtensions
    {
        public static string FormatIf(this AbstractConsumerSettings consumerSettings, Message msg, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            if (consumerSettings.GetKind() == PathKind.Queue)
            {
                return $"Queue: {consumerSettings.Topic}, SequenceNumber: {msg.SystemProperties.SequenceNumber}, DeliveryCount: {msg.SystemProperties.DeliveryCount}";
            }

            return $"Topic: {consumerSettings.Topic}, SubscriptionName: {consumerSettings.GetSubscriptionName()}, SequenceNumber: {msg.SystemProperties.SequenceNumber}, DeliveryCount: {msg.SystemProperties.DeliveryCount}";
        }

        public static string FormatIf(this ConsumerSettings consumerSettings, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            if (consumerSettings.GetKind() == PathKind.Queue)
            {
                return $"Queue: {consumerSettings.Topic}, MessageType: {consumerSettings.MessageType}";
            }

            return $"Topic: {consumerSettings.Topic}, SubscriptionName: {consumerSettings.GetSubscriptionName()}, MessageType: {consumerSettings.MessageType}";
        }

        public static string FormatIf(this AbstractConsumerSettings settings, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            if (settings.GetKind() == PathKind.Queue)
            {
                return $"Queue: {settings.Topic}";
            }
            return $"Topic: {settings.Topic}, SubscriptionName: {settings.GetSubscriptionName()}";
        }

    }
}
