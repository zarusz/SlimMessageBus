namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck;

static internal class ConsumerSettingsProperties
{
    /// <summary>
    /// <see cref="IConsumerCircuitBreaker"/> to be used with the consumer.
    /// </summary>
    static readonly internal ProviderExtensionProperty<TypeCollection<IConsumerCircuitBreaker>> CircuitBreakerTypes = new("CircuitBreaker_CircuitBreakerTypes");

    static readonly internal ProviderExtensionProperty<Dictionary<string, HealthStatus>> HealthStatusTags = new("CircuitBreaker_HealthStatusTags");
}
