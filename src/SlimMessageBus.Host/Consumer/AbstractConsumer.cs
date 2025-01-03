namespace SlimMessageBus.Host;

public abstract class AbstractConsumer : HasProviderExtensions, IAsyncDisposable, IConsumerControl
{
    private readonly SemaphoreSlim _semaphore;
    private readonly IReadOnlyList<IAbstractConsumerInterceptor> _interceptors;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _starting;
    private bool _stopping;

    public bool IsStarted { get; private set; }
    public string Path { get; }
    public ILogger Logger { get; }
    public IReadOnlyList<AbstractConsumerSettings> Settings { get; }
    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    protected AbstractConsumer(ILogger logger,
                               IEnumerable<AbstractConsumerSettings> consumerSettings,
                               string path,
                               IEnumerable<IAbstractConsumerInterceptor> interceptors)
    {
        _semaphore = new(1, 1);
        _interceptors = [.. interceptors.OrderBy(x => x.Order)];
        Logger = logger;
        Settings = [.. consumerSettings];
        Path = path;
    }

    private async Task<bool> CallInterceptor(Func<IAbstractConsumerInterceptor, Task<bool>> func)
    {
        foreach (var interceptor in _interceptors)
        {
            if (!await func(interceptor))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Starts the underyling transport consumer (synchronized).
    /// </summary>
    /// <returns></returns>
    public async Task DoStart()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await OnStart().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Stops the underyling transport consumer (synchronized).
    /// </summary>
    /// <returns></returns>
    public async Task DoStop()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await OnStop().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Start()
    {
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

            if (await CallInterceptor(x => x.CanStart(this)))
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
        if (!IsStarted || _stopping)
        {
            return;
        }

        await _semaphore.WaitAsync();
        _stopping = true;
        try
        {
            await _cancellationTokenSource.CancelAsync();

            if (await CallInterceptor(x => x.CanStop(this)))
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

    /// <summary>
    /// Initializes the transport specific consumer loop after the consumer has been started.
    /// </summary>
    /// <returns></returns>
    internal protected abstract Task OnStart();

    /// <summary>
    /// Destroys the transport specific consumer loop before the consumer is stopped.
    /// </summary>
    /// <returns></returns>
    internal protected abstract Task OnStop();

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
}
