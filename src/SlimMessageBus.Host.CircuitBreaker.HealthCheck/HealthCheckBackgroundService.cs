namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck;

internal sealed class HealthCheckBackgroundService : IHealthCheckPublisher, IHostedService, IHealthCheckHostBreaker, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Dictionary<string, HealthReportEntry> _healthReportEntries;
    private readonly List<OnChangeDelegate> _onChangeDelegates;
    private readonly SemaphoreSlim _semaphore;
    private IReadOnlyDictionary<string, HealthStatus> _tagStatus;

    public HealthCheckBackgroundService()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _healthReportEntries = [];
        _onChangeDelegates = [];
        _semaphore = new SemaphoreSlim(1, 1);
        _tagStatus = new Dictionary<string, HealthStatus>();
    }

    public IReadOnlyDictionary<string, HealthStatus> TagStatus => _tagStatus;

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken).Token;

        await _semaphore.WaitAsync(linkedToken);
        try
        {
            UpdateHealthReportEntries(report);
            if (UpdateTagStatus() && !linkedToken.IsCancellationRequested)
            {
                await Task.WhenAll(_onChangeDelegates.Select(x => x(_tagStatus)));
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    public async Task Subscribe(OnChangeDelegate onChange)
    {
        _onChangeDelegates.Add(onChange);
        await onChange(_tagStatus);
    }

    public void Unsubscribe(OnChangeDelegate onChange)
    {
        _onChangeDelegates.Remove(onChange);
    }

    private void UpdateHealthReportEntries(HealthReport report)
    {
        foreach (var entry in report.Entries.Where(x => x.Value.Tags.Any()))
        {
            _healthReportEntries[entry.Key] = entry.Value;
        }
    }

    private bool UpdateTagStatus()
    {
        var tagStatus = new Dictionary<string, HealthStatus>();
        foreach (var entry in _healthReportEntries.Values)
        {
            foreach (var tag in entry.Tags)
            {
                if (tagStatus.TryGetValue(tag, out var currentStatus))
                {
                    if ((entry.Status == HealthStatus.Degraded && currentStatus == HealthStatus.Healthy)
                        || (entry.Status == HealthStatus.Unhealthy && currentStatus != HealthStatus.Unhealthy))
                    {
                        tagStatus[tag] = entry.Status;
                    }

                    continue;
                }

                tagStatus[tag] = entry.Status;
            }
        }

        if (!AreEqual(_tagStatus, tagStatus))
        {
            _tagStatus = tagStatus;
            return true;
        }

        return false;
    }

    internal static bool AreEqual<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dict1, IReadOnlyDictionary<TKey, TValue> dict2)
    {
        if (dict1.Count != dict2.Count)
        {
            return false;
        }

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value) || !EqualityComparer<TValue>.Default.Equals(kvp.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
