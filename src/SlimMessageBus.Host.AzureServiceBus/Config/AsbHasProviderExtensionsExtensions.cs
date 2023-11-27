namespace SlimMessageBus.Host.AzureServiceBus;

using Azure.Messaging.ServiceBus.Administration;

static internal class AsbHasProviderExtensionsExtensions
{
    static internal HasProviderExtensions SetMessageModifier(this HasProviderExtensions producerSettings, AsbMessageModifier<object> messageModifierAction)
    {
        producerSettings.Properties[nameof(SetMessageModifier)] = messageModifierAction;
        return producerSettings;
    }

    static internal AsbMessageModifier<object> GetMessageModifier(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<AsbMessageModifier<object>>(nameof(SetMessageModifier), null);
    }

    static internal HasProviderExtensions SetQueueOptions(this HasProviderExtensions producerSettings, Action<CreateQueueOptions> optionsAction)
    {
        producerSettings.Properties[nameof(SetQueueOptions)] = optionsAction;
        return producerSettings;
    }

    static internal Action<CreateQueueOptions> GetQueueOptions(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<CreateQueueOptions>>(nameof(SetQueueOptions));
    }

    static internal HasProviderExtensions SetTopicOptions(this HasProviderExtensions producerSettings, Action<CreateTopicOptions> optionsAction)
    {
        producerSettings.Properties[nameof(SetTopicOptions)] = optionsAction;
        return producerSettings;
    }

    static internal Action<CreateTopicOptions> GetTopicOptions(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<CreateTopicOptions>>(nameof(SetTopicOptions));
    }

    static internal HasProviderExtensions SetSubscriptionOptions(this HasProviderExtensions producerSettings, Action<CreateSubscriptionOptions> optionsAction)
    {
        producerSettings.Properties[nameof(SetSubscriptionOptions)] = optionsAction;
        return producerSettings;
    }

    static internal Action<CreateSubscriptionOptions> GetSubscriptionOptions(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<CreateSubscriptionOptions>>(nameof(SetSubscriptionOptions));
    }
}