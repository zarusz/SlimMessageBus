using System;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    internal static class HasProviderExtensionsExtensions
    {
        internal static HasProviderExtensions SetKind(this HasProviderExtensions producerSettings, PathKind kind)
        {
            producerSettings.Properties["Kind"] = kind;
            return producerSettings;
        }

        internal static PathKind GetKind(this HasProviderExtensions producerSettings)
        {
            return producerSettings.GetOrDefault("Kind", PathKind.Topic);
        }

        internal static HasProviderExtensions SetMessageModifier(this HasProviderExtensions producerSettings, Action<object, Message> messageModifierAction)
        {
            producerSettings.Properties["MessageModifier"] = messageModifierAction;
            return producerSettings;
        }

        internal static Action<object, Message> GetMessageModifier(this HasProviderExtensions producerSettings)
        {
            return producerSettings.GetOrDefault<Action<object, Message>>("MessageModifier", (x, y) => { });
        }
    }
}