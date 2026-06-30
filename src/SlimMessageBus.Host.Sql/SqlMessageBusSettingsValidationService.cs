namespace SlimMessageBus.Host.Sql;

public class SqlMessageBusSettingsValidationService(MessageBusSettings settings, SqlMessageBusSettings providerSettings)
    : DefaultMessageBusSettingsValidationService(settings)
{
    public override void AssertSettings()
    {
        base.AssertSettings();

        if (string.IsNullOrWhiteSpace(providerSettings.ConnectionString))
        {
            throw new ConfigurationMessageBusException("The SQL provider requires a connection string");
        }

        if (providerSettings.PollBatchSize <= 0)
        {
            throw new ConfigurationMessageBusException("The SQL provider poll batch size must be greater than 0");
        }

        if (providerSettings.MaxDeliveryAttempts <= 0)
        {
            throw new ConfigurationMessageBusException("The SQL provider max delivery attempts must be greater than 0");
        }
    }
}
