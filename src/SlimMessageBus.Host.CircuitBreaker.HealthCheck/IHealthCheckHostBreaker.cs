namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck;

internal interface IHealthCheckHostBreaker
{
    Task Subscribe(OnChangeDelegate onChange);
    void Unsubscribe(OnChangeDelegate onChange);
}

public delegate Task OnChangeDelegate(IReadOnlyDictionary<string, HealthStatus> tags);