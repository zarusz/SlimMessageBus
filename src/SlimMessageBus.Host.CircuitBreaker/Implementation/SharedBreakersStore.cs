namespace SlimMessageBus.Host.CircuitBreaker;

using System.Collections.Concurrent;

/// <summary>
/// Stores shared circuit breaker instances to ensure consumers with identical circuit breaker configuration
/// share the same breaker instance. This enables coordinated behavior when multiple consumers monitor
/// the same external conditions (e.g., health checks).
/// </summary>
internal sealed class SharedBreakersStore
{
    private readonly ConcurrentDictionary<(Type BreakerType, string Key), IConsumerCircuitBreaker> _sharedBreakers = new();

    /// <summary>
    /// Gets or creates a shared circuit breaker instance for the given configuration.
    /// </summary>
    /// <param name="breakerType">The type of circuit breaker to create.</param>
    /// <param name="breakerKey">A unique key identifying the circuit breaker configuration.</param>
    /// <param name="serviceProvider">Service provider for creating the breaker instance.</param>
    /// <param name="consumerSettings">Consumer settings passed to the breaker constructor.</param>
    /// <returns>The shared circuit breaker instance.</returns>
    public IConsumerCircuitBreaker GetOrCreate(
        Type breakerType,
        string breakerKey,
        IServiceProvider serviceProvider,
        IEnumerable<AbstractConsumerSettings> consumerSettings)
    {
        var key = (breakerType, breakerKey);

        // Lock-free read for existing breakers
        // Only locks when adding a new breaker instance
        return _sharedBreakers.GetOrAdd(
            key,
            k => (IConsumerCircuitBreaker)ActivatorUtilities.CreateInstance(serviceProvider, k.BreakerType, consumerSettings));
    }

    /// <summary>
    /// Clears all shared circuit breaker instances. Used for testing.
    /// </summary>
    internal void Clear()
    {
        _sharedBreakers.Clear();
    }
}
