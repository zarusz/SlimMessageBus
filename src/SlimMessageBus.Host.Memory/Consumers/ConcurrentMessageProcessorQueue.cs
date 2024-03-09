namespace SlimMessageBus.Host.Memory;

using MessageItem = (object TransportMessage, IReadOnlyDictionary<string, object> MessageHeaders);

public class ConcurrentMessageProcessorQueue(IMessageProcessor<object> messageProcessor, ILogger<ConcurrentMessageProcessorQueue> logger, int concurrency, CancellationToken cancellationToken)
    : AbstractMessageProcessorQueue(messageProcessor, logger), IDisposable
{
    private bool _disposedValue;

    private readonly object _queueLock = new();
    private readonly Queue<MessageItem> _queue = new();

    private SemaphoreSlim _semaphore = new(concurrency, concurrency);

    public override void Enqueue(object transportMessage, IReadOnlyDictionary<string, object> messageHeaders)
    {
        lock (_queueLock)
        {
            _queue.Enqueue((transportMessage, messageHeaders));

            // Fire and forget
            _ = ProcessMessage();
        }
    }

    protected async Task ProcessMessage()
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            MessageItem item;
            lock (_queueLock)
            {
#if NETSTANDARD2_0
                if (_queue.Count == 0)
                {
                    return;
                }
                item = _queue.Dequeue();
#else
                if (!_queue.TryDequeue(out item))
                {
                    return;
                }
#endif
            }

            await ProcessMessage(item.TransportMessage, item.MessageHeaders, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore?.Release();
        }
    }

    #region Disposable

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _semaphore?.Dispose();
            _semaphore = null;
        }

        _disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
