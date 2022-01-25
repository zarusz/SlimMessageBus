namespace SlimMessageBus.Host.AzureServiceBus
{
    using System;
    using Azure.Messaging.ServiceBus;
    using SlimMessageBus.Host.Config;

    internal static class HasProviderExtensionsExtensions
    {
        internal static HasProviderExtensions SetMessageModifier(this HasProviderExtensions producerSettings, Action<object, ServiceBusMessage> messageModifierAction)
        {
            producerSettings.Properties[nameof(SetMessageModifier)] = messageModifierAction;
            return producerSettings;
        }

        internal static Action<object, ServiceBusMessage> GetMessageModifier(this HasProviderExtensions producerSettings)
        {
            return producerSettings.GetOrDefault<Action<object, ServiceBusMessage>>(nameof(SetMessageModifier), (x, y) => { });
        }
    }
}