namespace SlimMessageBus.Host.CircuitBreaker;

static internal class AbstractConsumerProperties
{
    static readonly internal ProviderExtensionProperty<bool> IsPaused = new("CircuitBreaker_IsPaused");
    static readonly internal ProviderExtensionProperty<List<IConsumerCircuitBreaker>?> Breakers = new("CircuitBreaker_Breakers");
}
