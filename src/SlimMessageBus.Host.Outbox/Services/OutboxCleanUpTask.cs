#nullable enable
namespace SlimMessageBus.Host.Outbox.Services;

public class OutboxCleanUpTask<TOutboxMessage> : IMessageBusLifecycleInterceptor, IAsyncDisposable
    where TOutboxMessage : OutboxMessage
{
    private readonly ILogger<OutboxCleanUpTask<TOutboxMessage>> _logger;
    private readonly OutboxSettings _outboxSettings;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _semaphore;

    private CancellationTokenSource? _cts;
    private Task? _executingTask;
    private int _busStartCount;

    public OutboxCleanUpTask(
        ILogger<OutboxCleanUpTask<TOutboxMessage>> logger,
        OutboxSettings outboxSettings,
        TimeProvider currentTimeProvider,
        IHostApplicationLifetime hostApplicationLifetime,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _outboxSettings = outboxSettings;
        _timeProvider = currentTimeProvider;
        _hostApplicationLifetime = hostApplicationLifetime;
        _serviceProvider = serviceProvider;
        _semaphore = new SemaphoreSlim(1);
    }

    public async ValueTask DisposeAsync()
    {
        await Shutdown();
        GC.SuppressFinalize(this);
    }

    public async Task OnBusLifecycle(MessageBusLifecycleEventType eventType, IMessageBus bus)
    {
        if (!_outboxSettings.MessageCleanup.Enabled)
        {
            return;
        }

        switch (eventType)
        {
            case MessageBusLifecycleEventType.Started:
                if (Interlocked.Increment(ref _busStartCount) != 1)
                {
                    return;
                }

                await Startup();
                break;

            case MessageBusLifecycleEventType.Stopping:
                if (Interlocked.Decrement(ref _busStartCount) != 0 || _executingTask == null)
                {
                    return;
                }

                await Shutdown();
                break;
        }
    }

    protected async Task CleanUpLoop(CancellationToken cancellationToken)
    {
        var batchSize = _outboxSettings.MessageCleanup.BatchSize;
        while (!cancellationToken.IsCancellationRequested)
        {
            var scope = _serviceProvider.CreateScope();
            try
            {
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository<TOutboxMessage>>();
                var timestamp = _timeProvider.GetUtcNow().Add(-_outboxSettings.MessageCleanup.Age);
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (await outboxRepository.DeleteSent(timestamp, batchSize, cancellationToken) < batchSize)
                    {
                        break;
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing outbox clean up");
            }
            finally
            {
                if (scope is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    scope.Dispose();
                }

                _logger.LogDebug("Outbox clean up loop stopped");
            }

            await Sleep(_outboxSettings.MessageCleanup.Interval, cancellationToken).ConfigureAwait(false);
        }
    }

    protected virtual async Task Sleep(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
#if !NET8_0_OR_GREATER
            await _timeProvider.Delay(delay, cancellationToken).ConfigureAwait(false);
#endif

#if NET8_0_OR_GREATER
            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
#endif
        }
        catch (TaskCanceledException)
        {
            // do nothing, will be evaluated in calling method
        }
    }

    private async Task Startup()
    {
        await _semaphore.WaitAsync(_hostApplicationLifetime.ApplicationStopping);
        try
        {
            Debug.Assert(_executingTask == null);
            Debug.Assert(_cts == null);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(_hostApplicationLifetime.ApplicationStopping);
            _executingTask = CleanUpLoop(_cts.Token);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task Shutdown()
    {
        if (_cts == null)
        {
            Debug.Assert(_executingTask == null);
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            if (_executingTask == null)
            {
                return;
            }

            try
            {
                await _cts!.CancelAsync();
                await _executingTask;
            }
            finally
            {
                _cts!.Dispose();
                _cts = null;
                _executingTask = null;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}