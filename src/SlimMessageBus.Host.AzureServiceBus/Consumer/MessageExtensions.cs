using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    internal static class MessageExtensions
    {
        public static string FormatIf(this ConsumerSettings consumerSettings, Message msg, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            return $"Topic: {consumerSettings.Topic}, SubscriptionName: {consumerSettings.GetSubscriptionName()}, SequenceNumber: {msg.SystemProperties.SequenceNumber}, DeliveryCount: {msg.SystemProperties.DeliveryCount}";
        }

        public static string FormatIf(this ConsumerSettings consumerSettings, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            return $"Topic: {consumerSettings.Topic}, SubscriptionName: {consumerSettings.GetSubscriptionName()}, MessageType: {consumerSettings.MessageType}";
        }
    }
}
