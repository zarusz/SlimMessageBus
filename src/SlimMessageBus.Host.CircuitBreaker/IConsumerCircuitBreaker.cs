namespace SlimMessageBus.Host.CircuitBreaker;

/// <summary>
/// Circuit breaker to toggle consumer status on an external event.
/// </summary>
public interface IConsumerCircuitBreaker
{
    Circuit State { get; }
    Task Subscribe(Func<Circuit, Task> onChange);
    void Unsubscribe();
}
