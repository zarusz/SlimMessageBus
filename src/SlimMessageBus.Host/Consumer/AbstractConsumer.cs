namespace SlimMessageBus.Host;

public abstract class AbstractConsumer : IAsyncDisposable, IConsumerControl
{
    private readonly SemaphoreSlim _semaphore;
    private readonly List<IConsumerCircuitBreaker> _circuitBreakers;

    private CancellationTokenSource _cancellationTokenSource;
    private bool _starting;
    private bool _stopping;

    public bool IsPaused { get; private set; }
    public bool IsStarted { get; private set; }
    protected ILogger Logger { get; }
    protected IReadOnlyList<AbstractConsumerSettings> Settings { get; }
    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    protected AbstractConsumer(ILogger logger, IEnumerable<AbstractConsumerSettings> consumerSettings)
    {
        _semaphore = new(1, 1);
        _circuitBreakers = [];

        Logger = logger;
        Settings = consumerSettings.ToList();
    }

    public async Task Start()
    {
        async Task StartCircuitBreakers()
        {
            var types = Settings.SelectMany(x => x.CircuitBreakers).Distinct();
            if (!types.Any())
            {
                return;
            }

            var sp = Settings.Select(x => x.MessageBusSettings.ServiceProvider).FirstOrDefault(x => x != null);
            foreach (var type in types.Distinct())
            {
                var breaker = (IConsumerCircuitBreaker)ActivatorUtilities.CreateInstance(sp, type, Settings);
                _circuitBreakers.Add(breaker);
                await breaker.Subscribe(BreakerChanged);
            }
        }

        if (IsStarted || _starting)
        {
            return;
        }

        await _semaphore.WaitAsync();
        _starting = true;
        try
        {
            if (_cancellationTokenSource?.IsCancellationRequested != false)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            await StartCircuitBreakers();
            IsPaused = _circuitBreakers.Exists(x => x.State == Circuit.Closed);
            if (!IsPaused)
            {
                await OnStart().ConfigureAwait(false);
            }

            IsStarted = true;
        }
        finally
        {
            _starting = false;
            _semaphore.Release();
        }
    }

    public async Task Stop()
    {
        async Task StopCircuitBreakers()
        {
            foreach (var breaker in _circuitBreakers)
            {
                breaker.Unsubscribe();

                if (breaker is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (breaker is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _circuitBreakers.Clear();
        }

        if (!IsStarted || _stopping)
        {
            return;
        }

        await _semaphore.WaitAsync();
        _stopping = true;
        try
        {
            await _cancellationTokenSource.CancelAsync();

            await StopCircuitBreakers();
            if (!IsPaused)
            {
                await OnStop().ConfigureAwait(false);
            }

            IsStarted = false;
        }
        finally
        {
            _stopping = false;
            _semaphore.Release();
        }
    }

    protected abstract Task OnStart();
    protected abstract Task OnStop();

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected async virtual ValueTask DisposeAsyncCore()
    {
        await Stop().ConfigureAwait(false);

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    #endregion

    async internal Task BreakerChanged(Circuit state)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!IsStarted)
            {
                return;
            }

            var shouldPause = state == Circuit.Closed || _circuitBreakers.Exists(x => x.State == Circuit.Closed);
            if (shouldPause != IsPaused)
            {
                var settings = Settings.Count > 0 ? Settings[0] : null;
                var path = settings?.Path ?? "[unknown path]";
                var bus = settings?.MessageBusSettings?.Name ?? "default";
                if (shouldPause)
                {
                    Logger.LogWarning("Circuit breaker tripped for '{Path}' on '{Bus}' bus. Consumer paused.", path, bus);
                    await OnStop().ConfigureAwait(false);
                }
                else
                {
                    Logger.LogInformation("Circuit breaker restored for '{Path}' on '{Bus}' bus. Consumer resumed.", path, bus);
                    await OnStart().ConfigureAwait(false);
                }

                IsPaused = shouldPause;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
