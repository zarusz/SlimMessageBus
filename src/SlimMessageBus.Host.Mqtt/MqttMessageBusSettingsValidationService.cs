namespace SlimMessageBus.Host.Mqtt;

internal class MqttMessageBusSettingsValidationService : DefaultMessageBusSettingsValidationService<MqttMessageBusSettings>
{
    public MqttMessageBusSettingsValidationService(MessageBusSettings settings, MqttMessageBusSettings providerSettings) : base(settings, providerSettings)
    {
    }

    public override void AssertSettings()
    {
        base.AssertSettings();

        if (ProviderSettings.ClientBuilder is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(MqttMessageBusSettings)}.{nameof(MqttMessageBusSettings.ClientBuilder)} must be set");
        }
        if (ProviderSettings.ManagedClientBuilder is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(MqttMessageBusSettings)}.{nameof(MqttMessageBusSettings.ManagedClientBuilder)} must be set");
        }
    }
}
