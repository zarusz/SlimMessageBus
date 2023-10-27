namespace SlimMessageBus.Host.Redis;

using SlimMessageBus.Host.Services;

internal class RedisMessageBusSettingsValidationService : DefaultMessageBusSettingsValidationService<RedisMessageBusSettings>
{
    public RedisMessageBusSettingsValidationService(MessageBusSettings settings, RedisMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
    }

    public override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrEmpty(ProviderSettings.ConnectionString))
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(RedisMessageBusSettings)}.{nameof(RedisMessageBusSettings.ConnectionString)} must be set");
        }

        if (ProviderSettings.EnvelopeSerializer is null)
        {
            throw new ConfigurationMessageBusException(Settings, $"The {nameof(RedisMessageBusSettings)}.{nameof(RedisMessageBusSettings.EnvelopeSerializer)} must be set");
        }
    }
}
