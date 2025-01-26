namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck;

using SlimMessageBus.Host.CircuitBreaker;

internal sealed class HealthCheckCircuitBreaker : IConsumerCircuitBreaker
{
    private readonly IEnumerable<AbstractConsumerSettings> _settings;
    private readonly IHealthCheckHostBreaker _host;
    private Func<Circuit, Task>? _onChange;
    private IDictionary<string, HealthStatus>? _monitoredTags;

    public HealthCheckCircuitBreaker(IEnumerable<AbstractConsumerSettings> settings, IHealthCheckHostBreaker host)
    {
        _settings = settings;
        _host = host;
        State = Circuit.Open;
    }

    public Circuit State { get; private set; }

    public async Task Subscribe(Func<Circuit, Task> onChange)
    {
        Debug.Assert(_onChange == null);
        _onChange = onChange;

        _monitoredTags = _settings
            .Select(x => x.HealthBreakerTags())
            .Aggregate(
                (a, b) =>
                {
                    var c = new Dictionary<string, HealthStatus>(a.Count + b.Count);
                    foreach (var kvp in a)
                    {
                        var status = kvp.Value;
                        if (b.TryGetValue(kvp.Key, out var altStatus))
                        {
                            b.Remove(kvp.Key);
                            if (status != altStatus && altStatus == HealthStatus.Degraded)
                            {
                                status = HealthStatus.Degraded;
                            }
                        }

                        c[kvp.Key] = status;
                    }

                    foreach (var kvp in b)
                    {
                        c.Add(kvp.Key, kvp.Value);
                    }

                    return c;
                });

        await _host.Subscribe(TagStatusChanged);
    }

    public void Unsubscribe()
    {
        _host.Unsubscribe(TagStatusChanged);
        _onChange = null;
        _monitoredTags = null;
    }

    internal async Task TagStatusChanged(IReadOnlyDictionary<string, HealthStatus> tags)
    {
        var newState = _monitoredTags!
            .All(
                monitoredTag =>
                {
                    if (!tags.TryGetValue(monitoredTag.Key, out var currentStatus))
                    {
                        // unknown tag, assume healthy
                        return true;
                    }

                    return currentStatus switch
                    {
                        HealthStatus.Healthy => true,
                        HealthStatus.Degraded => monitoredTag.Value != HealthStatus.Degraded,
                        HealthStatus.Unhealthy => false,
                        _ => throw new InvalidOperationException($"Unknown health status '{currentStatus}'")
                    };
                })
            ? Circuit.Open
            : Circuit.Closed;

        if (State != newState)
        {
            State = newState;
            await _onChange!(newState);
        }
    }
}
