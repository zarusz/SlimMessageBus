using System;
using Microsoft.Azure.ServiceBus;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureServiceBus
{
    internal static class HasProviderExtensionsExtensions
    {
        internal static HasProviderExtensions SetKind(this HasProviderExtensions producerSettings, PathKind kind)
        {
            producerSettings.Properties[nameof(SetKind)] = kind;
            return producerSettings;
        }

        internal static PathKind GetKind(this HasProviderExtensions producerSettings)
        {
            return producerSettings.GetOrDefault(nameof(SetKind), PathKind.Topic);
        }

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