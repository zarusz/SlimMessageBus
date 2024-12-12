namespace SlimMessageBus.Host;

/// <summary>
/// Manages the pending requests - ensure requests which exceeded the allotted timeout period are removed.
/// </summary>
public class PendingRequestManager : IPendingRequestManager, IDisposable
{
    private readonly ILogger _logger;

    private readonly Timer _timer;
    private readonly object _timerSync = new();

    private readonly ICurrentTimeProvider _timeProvider;
    private readonly Action<object> _onRequestTimeout;
    private bool _cleanInProgress;

    public IPendingRequestStore Store { get; }

    public PendingRequestManager(IPendingRequestStore store, ICurrentTimeProvider timeProvider, ILoggerFactory loggerFactory, TimeSpan? interval = null, Action<object> onRequestTimeout = null)
    {
        _logger = loggerFactory.CreateLogger<PendingRequestManager>();
        Store = store;

        _onRequestTimeout = onRequestTimeout;
        _timeProvider = timeProvider;

        var timerInterval = interval ?? TimeSpan.FromSeconds(3);
        _timer = new Timer(state => TimerCallback(), null, timerInterval, timerInterval);
    }

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timer.Dispose();
        }
    }

    #endregion

    private void TimerCallback()
    {
        // Note: timer callback calls are reentrant, so we need to ensure only one clean happens at a time.

        lock (_timerSync)
        {
            if (_cleanInProgress)
            {
                return;
            }
            _cleanInProgress = true;
        }

        CleanPendingRequests();

        lock (_timerSync)
        {
            _cleanInProgress = false;
        }
    }

    /// <summary>
    /// Performs cleanup of pending requests (requests that timed out or were cancelled).
    /// </summary>
    public virtual void CleanPendingRequests()
    {
        var now = _timeProvider.CurrentTime;

        var requestsToCancel = Store.FindAllToCancel(now);
        foreach (var requestState in requestsToCancel)
        {
            // request is either cancelled (via CancellationToken) or expired
            var canceled = requestState.CancellationToken.IsCancellationRequested
                ? requestState.TaskCompletionSource.TrySetCanceled(requestState.CancellationToken)
                : requestState.TaskCompletionSource.TrySetCanceled();

            if (canceled)
            {
                _logger.LogDebug("Pending request timed-out: {RequestState}, now: {TimeNow}", requestState, now);
                _onRequestTimeout?.Invoke(requestState.Request);
            }
        }
        Store.RemoveAll(requestsToCancel.Select(x => x.Id));
    }
}
