namespace SlimMessageBus.Host;

public abstract class AbstractConsumer : IAsyncDisposable, IConsumerControl
{
    private CancellationTokenSource _cancellationTokenSource;
    private bool _starting;
    private bool _stopping;

    protected ILogger Logger { get; }

    public bool IsStarted { get; private set; }

    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    protected AbstractConsumer(ILogger logger)
    {
        Logger = logger;
    }

    public async Task Start()
    {
        if (IsStarted || _starting)
        {
            return;
        }

        _starting = true;
        try
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
            }

            await OnStart().ConfigureAwait(false);

            IsStarted = true;
        }
        finally
        {
            _starting = false;
        }
    }

    public async Task Stop()
    {
        if (!IsStarted || _stopping)
        {
            return;
        }

        _stopping = true;
        try
        {
            _cancellationTokenSource.Cancel();

            await OnStop().ConfigureAwait(false);

            IsStarted = false;
        }
        finally
        {
            _stopping = false;
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
}
