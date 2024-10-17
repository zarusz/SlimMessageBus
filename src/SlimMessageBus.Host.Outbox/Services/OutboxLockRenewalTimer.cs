namespace SlimMessageBus.Host.Outbox.Services;

internal sealed class OutboxLockRenewalTimer<TOutboxMessage, TOutboxMessageKey> : IOutboxLockRenewalTimer
    where TOutboxMessage : OutboxMessage<TOutboxMessageKey>
{
    private readonly object _lock;
    private readonly Timer _timer;
    private readonly ILogger<OutboxLockRenewalTimer<TOutboxMessage, TOutboxMessageKey>> _logger;
    private readonly IOutboxMessageRepository<TOutboxMessage, TOutboxMessageKey> _outboxRepository;
    private readonly CancellationToken _cancellationToken;
    private readonly Action<Exception> _lockLost;
    private bool _active;
    private bool _renewingLock;

    public OutboxLockRenewalTimer(ILogger<OutboxLockRenewalTimer<TOutboxMessage, TOutboxMessageKey>> logger, IOutboxMessageRepository<TOutboxMessage, TOutboxMessageKey> outboxRepository, IInstanceIdProvider instanceIdProvider, TimeSpan lockDuration, TimeSpan lockRenewalInterval, Action<Exception> lockLost, CancellationToken cancellationToken)
    {
        Debug.Assert(lockRenewalInterval < lockDuration);

        _logger = logger;
        _outboxRepository = outboxRepository;
        InstanceId = instanceIdProvider.GetInstanceId();
        LockDuration = lockDuration;
        RenewalInterval = lockRenewalInterval;
        _cancellationToken = cancellationToken;
        _lockLost = lockLost;

        _lock = new object();
        _timer = new Timer(async _ => await CallbackAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _active = false;
        _cancellationToken.Register(Stop);
    }

    public bool Active => _active;
    public string InstanceId { get; }
    public TimeSpan LockDuration { get; }
    public TimeSpan RenewalInterval { get; }

    public void Start()
    {
        lock (_lock)
        {
            _active = true;
            if (!_renewingLock)
            {
                _timer.Change(RenewalInterval, RenewalInterval);
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _active = false;
        }
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();

        GC.SuppressFinalize(this);
    }

    private async Task CallbackAsync()
    {
        lock (_lock)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            if (!_active)
            {
                return;
            }

            _renewingLock = true;
        }

        try
        {
            try
            {
                try
                {
                    if (!await _outboxRepository.RenewLock(InstanceId, LockDuration, _cancellationToken))
                    {
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        // NOTE: There is a small chance that renew will fire after all messages have
                        // been completed/aborted, but before the timer has been instructed to stop.
                        // This will lead to a false failure in the logs.

                        _logger.LogWarning("Failed to renew lock");
                        throw new LockLostException($"Unable to renew lock for instance {InstanceId}");
                    }

                    _logger.LogDebug("Lock renewed for instance {InstanceId}", InstanceId);
                }
                catch (Exception ex) when (ex is not LockLostException)
                {
                    _logger.LogError(ex, "Failed to renew lock");
                    throw new LockLostException("An exception occurred while attempting to renew the lock", ex);
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _active = false;
                }

                _lockLost(ex);
                return;
            }

            lock (_lock)
            {
                if (_active)
                {
                    _timer.Change(RenewalInterval, RenewalInterval);
                }
            }
        }
        finally
        {
            _renewingLock = false;
        }
    }

    public class LockLostException : Exception
    {
        public LockLostException(string message)
            : base(message)
        {
        }

        public LockLostException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}