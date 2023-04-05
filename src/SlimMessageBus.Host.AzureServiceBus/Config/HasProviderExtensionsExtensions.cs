namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

internal static class HasProviderExtensionsExtensions
{
    internal static HasProviderExtensions SetMessageModifier(this HasProviderExtensions producerSettings, Action<object, ServiceBusMessage> messageModifierAction)
    {
        producerSettings.Properties[nameof(SetMessageModifier)] = messageModifierAction;
        return producerSettings;
    }

    internal static Action<object, ServiceBusMessage> GetMessageModifier(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<object, ServiceBusMessage>>(nameof(SetMessageModifier), null);
    }

    internal static HasProviderExtensions SetQueueOptions(this HasProviderExtensions producerSettings, Action<CreateQueueOptions> optionsAction)
    {
        producerSettings.Properties[nameof(SetQueueOptions)] = optionsAction;
        return producerSettings;
    }

    internal static Action<CreateQueueOptions> GetQueueOptions(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<CreateQueueOptions>>(nameof(SetQueueOptions));
    }

    internal static HasProviderExtensions SetTopicOptions(this HasProviderExtensions producerSettings, Action<CreateTopicOptions> optionsAction)
    {
        producerSettings.Properties[nameof(SetTopicOptions)] = optionsAction;
        return producerSettings;
    }

    internal static Action<CreateTopicOptions> GetTopicOptions(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<CreateTopicOptions>>(nameof(SetTopicOptions));
    }

    internal static HasProviderExtensions SetSubscriptionOptions(this HasProviderExtensions producerSettings, Action<CreateSubscriptionOptions> optionsAction)
    {
        producerSettings.Properties[nameof(SetSubscriptionOptions)] = optionsAction;
        return producerSettings;
    }

    internal static Action<CreateSubscriptionOptions> GetSubscriptionOptions(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<CreateSubscriptionOptions>>(nameof(SetSubscriptionOptions));
    }
}