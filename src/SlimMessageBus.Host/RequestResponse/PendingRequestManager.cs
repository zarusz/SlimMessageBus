using System;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SlimMessageBus.Host
{
    /// <summary>
    /// Manages the pending requests - ensure requests which exceeded the allotted timeout period are removed.
    /// </summary>
    public class PendingRequestManager : IDisposable
    {
        private readonly ILogger _logger;

        private readonly Timer _timer;
        private readonly object _timerSync = new object();
        private readonly TimeSpan _timerInterval;

        private readonly Func<DateTimeOffset> _timeProvider;
        private readonly Action<object> _onRequestTimeout;
        private bool _cleanInProgress;

        public IPendingRequestStore Store { get; }

        public PendingRequestManager(IPendingRequestStore store, Func<DateTimeOffset> timeProvider, TimeSpan interval, ILoggerFactory loggerFactory, Action<object> onRequestTimeout)
        {
            _logger = loggerFactory.CreateLogger<PendingRequestManager>();
            Store = store;

            _onRequestTimeout = onRequestTimeout;
            _timeProvider = timeProvider;
            _timerInterval = interval;
            _timer = new Timer(state => TimerCallback(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, _timerInterval);
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
            var now = _timeProvider();

            var requestsToCancel = Store.FindAllToCancel(now);
            foreach (var requestState in requestsToCancel)
            {
                // request is either cancelled (via CancellationToken) or expired
                var canceled = requestState.CancellationToken.IsCancellationRequested
                    ? requestState.TaskCompletionSource.TrySetCanceled(requestState.CancellationToken)
                    : requestState.TaskCompletionSource.TrySetCanceled();

                if (Store.Remove(requestState.Id) && canceled)
                {
                    _logger.LogDebug("Pending request timed-out: {0}, now: {1}", requestState, now);
                    _onRequestTimeout(requestState.Request);
                }
            }
        }
    }
}
