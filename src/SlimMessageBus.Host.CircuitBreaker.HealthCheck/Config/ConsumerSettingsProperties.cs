namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck;

static internal class ConsumerSettingsProperties
{
    static readonly internal ProviderExtensionProperty<Dictionary<string, HealthStatus>> HealthStatusTags = new("CircuitBreaker_HealthStatusTags");
}
