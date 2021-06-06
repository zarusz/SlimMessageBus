namespace SlimMessageBus.Host.AzureServiceBus
{
    using System;
    using Microsoft.Azure.ServiceBus;
    using SlimMessageBus.Host.Config;

    internal static class HasProviderExtensionsExtensions
    {
        internal static HasProviderExtensions SetMessageModifier(this HasProviderExtensions producerSettings, Action<object, Message> messageModifierAction)
        {
            producerSettings.Properties[nameof(SetMessageModifier)] = messageModifierAction;
            return producerSettings;
        }

        internal static Action<object, Message> GetMessageModifier(this HasProviderExtensions producerSettings)
        {
            return producerSettings.GetOrDefault<Action<object, Message>>(nameof(SetMessageModifier), (x, y) => { });
        }
    }
}