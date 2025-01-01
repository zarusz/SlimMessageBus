namespace SlimMessageBus.Host.CircuitBreaker;

static internal class ConsumerSettingsProperties
{
    /// <summary>
    /// <see cref="IConsumerCircuitBreaker"/> to be used with the consumer.
    /// </summary>
    static readonly internal ProviderExtensionProperty<TypeCollection<IConsumerCircuitBreaker>> CircuitBreakerTypes = new("CircuitBreaker_Types");
}
