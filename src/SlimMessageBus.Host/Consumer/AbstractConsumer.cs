namespace SlimMessageBus.Host;

public abstract partial class AbstractConsumer : HasProviderExtensions, IAsyncDisposable, IConsumerControl
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly IReadOnlyList<IAbstractConsumerInterceptor> _interceptors;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _starting;
    private bool _stopping;

    public bool IsStarted { get; private set; }
    public string Path { get; }
    public IReadOnlyList<AbstractConsumerSettings> Settings { get; }
    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;
    protected ILogger Logger => _logger;

    protected AbstractConsumer(ILogger logger,
                               IEnumerable<AbstractConsumerSettings> consumerSettings,
                               string path,
                               IEnumerable<IAbstractConsumerInterceptor> interceptors)
    {
        _semaphore = new(1, 1);
        _interceptors = [.. interceptors.OrderBy(x => x.Order)];
        _logger = logger;
        Settings = [.. consumerSettings];
        Path = path;
    }

    private async Task<bool> CallInterceptor(Func<IAbstractConsumerInterceptor, Task<bool>> func)
    {
        foreach (var interceptor in _interceptors)
        {
            try
            {
                if (!await func(interceptor).ConfigureAwait(false))
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogInterceptorFailed(interceptor.GetType(), e.Message, e);
            }
        }
        return true;
    }

    /// <summary>
    /// Starts the underlying transport consumer (synchronized).
    /// </summary>
    /// <returns></returns>
    public async Task DoStart()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await InternalOnStart().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task InternalOnStart()
    {
        await OnStart().ConfigureAwait(false);
        await CallInterceptor(async x => { await x.Started(this); return true; }).ConfigureAwait(false);
    }

    private async Task InternalOnStop()
    {
        await OnStop().ConfigureAwait(false);
        await CallInterceptor(async x => { await x.Stopped(this); return true; }).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the underlying transport consumer (synchronized).
    /// </summary>
    /// <returns></returns>
    public async Task DoStop()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await InternalOnStop().ConfigureAwait(false);
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

            if (await CallInterceptor(x => x.CanStart(this)).ConfigureAwait(false))
            {
                await InternalOnStart().ConfigureAwait(false);
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
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

            if (await CallInterceptor(x => x.CanStop(this)).ConfigureAwait(false))
            {
                await InternalOnStop().ConfigureAwait(false);
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

    #region Logging 

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Error,
       Message = "Interceptor {InterceptorType} failed with error: {Error}")]
    private partial void LogInterceptorFailed(Type interceptorType, string error, Exception ex);

    #endregion
}


#if NETSTANDARD2_0

public partial class AbstractConsumer
{
    private partial void LogInterceptorFailed(Type interceptorType, string error, Exception ex)
        => _logger.LogError(ex, "Interceptor {InterceptorType} failed with error: {Error}", interceptorType, error);
}

#endif