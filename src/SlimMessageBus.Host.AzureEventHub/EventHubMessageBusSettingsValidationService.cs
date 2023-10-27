namespace SlimMessageBus.Host.AzureEventHub;

internal class EventHubMessageBusSettingsValidationService : DefaultMessageBusSettingsValidationService<EventHubMessageBusSettings>
{
    public EventHubMessageBusSettingsValidationService(MessageBusSettings settings, EventHubMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
    }

    public override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrEmpty(ProviderSettings.ConnectionString))
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.ConnectionString)} must be set");
        }

        if (Settings.IsAnyConsumerDeclared())
        {
            if (string.IsNullOrEmpty(ProviderSettings.StorageConnectionString))
            {
                throw new ConfigurationMessageBusException(Settings, $"When consumers are declared, the {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.StorageConnectionString)} must be set");
            }
            if (string.IsNullOrEmpty(ProviderSettings.StorageBlobContainerName))
            {
                throw new ConfigurationMessageBusException(Settings, $"When consumers are declared, the {nameof(EventHubMessageBusSettings)}.{nameof(EventHubMessageBusSettings.StorageBlobContainerName)} must be set");
            }
        }
    }
}
