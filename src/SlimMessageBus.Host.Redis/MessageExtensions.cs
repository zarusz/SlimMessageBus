using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus.Consumer
{
    internal static class MessageExtensions
    {
        public static string FormatIf(this ConsumerSettings consumerSettings, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            return $"Topic: {consumerSettings.Topic}, MessageType: {consumerSettings.MessageType}";
        }

        public static string FormatIf(this AbstractConsumerSettings settings, bool logLevel)
        {
            if (!logLevel)
            {
                return string.Empty;
            }

            return $"Topic: {settings.Topic}";
        }

    }
}
