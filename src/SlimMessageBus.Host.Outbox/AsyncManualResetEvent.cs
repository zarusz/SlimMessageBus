namespace SlimMessageBus.Host.Outbox;

public sealed class AsyncManualResetEvent
{
    private readonly object _lock;
    private TaskCompletionSource<bool> _tcs;

    public AsyncManualResetEvent(bool initialState = false)
    {
        _lock = new object();
        _tcs = new TaskCompletionSource<bool>();

        if (initialState)
        {
            _tcs.SetResult(true);
        }
    }

    public async Task<bool> Wait(int millisecondsDelay = -1, CancellationToken cancellationToken = default)
    {
        Task GetTask()
        {
            lock (_lock)
            {
                return _tcs.Task;
            }
        }

        var resetEvent = GetTask();
        var task = await Task.WhenAny(resetEvent, Task.Delay(millisecondsDelay, cancellationToken));

        return task == resetEvent;
    }

    public Task<bool> Wait(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return Wait((int)delay.TotalMilliseconds, cancellationToken);
    }

    public void Set()
    {
        lock (_lock)
        {
            var tcs = _tcs;
            tcs.TrySetResult(true);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            if (_tcs.Task.IsCompleted)
            {
                _tcs = new TaskCompletionSource<bool>();
            }
        }
    }
}
