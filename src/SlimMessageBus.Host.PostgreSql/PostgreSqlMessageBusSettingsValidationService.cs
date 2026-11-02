namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlMessageBusSettingsValidationService(MessageBusSettings settings, PostgreSqlMessageBusSettings providerSettings)
    : DefaultMessageBusSettingsValidationService(settings)
{
    public override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrWhiteSpace(providerSettings.ConnectionString))
        {
            throw new ConfigurationMessageBusException("The PostgreSQL provider requires a connection string");
        }

        if (providerSettings.PollImmediateDuration < TimeSpan.Zero)
        {
            throw new ConfigurationMessageBusException("The PostgreSQL provider poll immediate duration must not be negative");
        }

        if (providerSettings.PollBatchSize <= 0)
        {
            throw new ConfigurationMessageBusException("The PostgreSQL provider poll batch size must be greater than 0");
        }

        if (providerSettings.MaxDeliveryAttempts <= 0)
        {
            throw new ConfigurationMessageBusException("The PostgreSQL provider max delivery attempts must be greater than 0");
        }
    }
}
