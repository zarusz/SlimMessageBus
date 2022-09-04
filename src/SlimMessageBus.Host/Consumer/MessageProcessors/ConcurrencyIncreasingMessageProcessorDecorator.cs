namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Config;

/// <summary>
/// Decorator for <see cref="IMessageProcessor{TMessage}"> that increases the amount of messages being concurrently processed.
/// The expectation is that <see cref="IMessageProcessor{TMessage}.ProcessMessage(TMessage)"/> will be executed synchronously (in sequential order) by the caller on which we want to increase amount of concurrent message being processed.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public class ConcurrencyIncreasingMessageProcessorDecorator<TMessage> : IMessageProcessor<TMessage>
{
    private readonly ILogger _logger;
    private SemaphoreSlim _concurrentSemaphore;
    private readonly IMessageProcessor<TMessage> _target;
    private Exception _lastException;
    private TMessage _lastExceptionMessage;
    private AbstractConsumerSettings _lastExceptionSettings;
    private readonly object _lastExceptionLock = new();

    private int _pendingCount;

    public int PendingCount => _pendingCount;

    public ConcurrencyIncreasingMessageProcessorDecorator(int concurrency, MessageBusBase messageBus, IMessageProcessor<TMessage> target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));
        if (concurrency <= 1) throw new ArgumentOutOfRangeException(nameof(concurrency));

        _logger = messageBus.LoggerFactory.CreateLogger<ConcurrencyIncreasingMessageProcessorDecorator<TMessage>>();
        _concurrentSemaphore = new SemaphoreSlim(concurrency);
        _target = target;
    }

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _target.ConsumerSettings;

    public async Task<(Exception Exception, AbstractConsumerSettings ConsumerSettings, object Response)> ProcessMessage(TMessage message, IReadOnlyDictionary<string, object> messageHeaders)
    {
        // Ensure only desired number of messages are being processed concurrently
        await _concurrentSemaphore.WaitAsync().ConfigureAwait(false);

        // Check if there was an exception from and earlier message processing
        var e = _lastException;
        if (e != null)
        {
            // report the last exception
            _lastException = null;
            return (e, _lastExceptionSettings, null);
        }

        Interlocked.Increment(ref _pendingCount);

        // Fire and forget
        _ = ProcessInBackground(message, messageHeaders);

        // Not exception - we don't know yet
        return (null, null, null);
    }

    public TMessage GetMessageWithException()
    {
        lock (_lastExceptionLock)
        {
            var m = _lastExceptionMessage;
            _lastExceptionMessage = default;
            return m;
        }
    }

    public async Task WaitAll()
    {
        while (_pendingCount > 0)
        {
            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    private async Task ProcessInBackground(TMessage message, IReadOnlyDictionary<string, object> messageHeaders)
    {
        try
        {
            _logger.LogDebug("Entering ProcessMessages for message {MessageType}", typeof(TMessage));
            var (exception, consumerSettings, response) = await _target.ProcessMessage(message, messageHeaders).ConfigureAwait(false);
            if (exception != null)
            {
                lock (_lastExceptionLock)
                {
                    // ensure there was no error before this one, in which case forget about this error (the whole event stream will be rewind back).
                    if (_lastException == null && _lastExceptionMessage == null)
                    {
                        _lastException = exception;
                        _lastExceptionMessage = message;
                        _lastExceptionSettings = consumerSettings;
                    }
                }
            }
        }
        finally
        {
            _logger.LogDebug("Leaving ProcessMessages for message {MessageType}", typeof(TMessage));
            _concurrentSemaphore.Release();

            Interlocked.Decrement(ref _pendingCount);
        }
    }

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_concurrentSemaphore != null)
        {
            _concurrentSemaphore.Dispose();
            _concurrentSemaphore = null;
        }
        await _target.DisposeAsync();
    }

    #endregion
}