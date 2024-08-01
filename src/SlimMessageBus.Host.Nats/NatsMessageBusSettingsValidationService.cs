namespace SlimMessageBus.Host.Nats;

public class NatsMessageBusSettingsValidationService(MessageBusSettings settings, NatsMessageBusSettings providerSettings)
    : DefaultMessageBusSettingsValidationService<NatsMessageBusSettings>(settings, providerSettings)
{
    public override void AssertSettings()
    {
        base.AssertSettings();

        if (ProviderSettings.ClientName is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(NatsMessageBusSettings)}.{nameof(NatsMessageBusSettings.ClientName)} must be set");
        }

        if (ProviderSettings.Endpoint is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(NatsMessageBusSettings)}.{nameof(NatsMessageBusSettings.Endpoint)} must be set");
        }

        if (ProviderSettings.AuthOpts is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(NatsMessageBusSettings)}.{nameof(NatsMessageBusSettings.AuthOpts)} must be set");
        }
    }
}