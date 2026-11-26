namespace SlimMessageBus.Host;

/// <summary>
/// Decorator for <see cref="IMessageProcessor{TMessage}"> that increases the amount of messages being concurrently processed.
/// The expectation is that <see cref="IMessageProcessor{TMessage}.ProcessMessage(TMessage)"/> will be executed synchronously (in sequential order) by the caller on which we want to increase amount of concurrent transportMessage being processed.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public sealed partial class ConcurrentMessageProcessorDecorator<TMessage> : IMessageProcessor<TMessage>, IDisposable
{
    private readonly ILogger _logger;
    private SemaphoreSlim _concurrentSemaphore;
    private readonly IMessageProcessor<TMessage> _target;
    private readonly bool _reportBackgroundExceptions;

    private int _pendingCount;

    public int PendingCount => _pendingCount;

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _target.ConsumerSettings;

    public ConcurrentMessageProcessorDecorator(int concurrency, ILoggerFactory loggerFactory, IMessageProcessor<TMessage> target, bool reportBackgroundExceptions = true)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));
        if (concurrency <= 0) throw new ArgumentOutOfRangeException(nameof(concurrency));

        _logger = loggerFactory.CreateLogger<ConcurrentMessageProcessorDecorator<TMessage>>();
        _concurrentSemaphore = new SemaphoreSlim(concurrency);
        _target = target;
        _reportBackgroundExceptions = reportBackgroundExceptions;
    }

    #region IDisposable

    public void Dispose()
    {
        if (_target is IDisposable targetDisposable)
        {
            targetDisposable.Dispose();
        }

        _concurrentSemaphore?.Dispose();
        _concurrentSemaphore = null;
    }

    #endregion

    public async Task<ProcessMessageResult> ProcessMessage(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        // Ensure only desired number of messages are being processed concurrently
        await _concurrentSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        Interlocked.Increment(ref _pendingCount);

        // Fire and forget
        _ = ProcessInBackground(transportMessage, messageHeaders, currentServiceProvider, consumerContextProperties, cancellationToken);

        // Not exception - we don't know yet
        return new(ProcessResult.Success, null, null, null);
    }

    /// <summary>
    /// Waits on all processing tasks to finish.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task WaitAll(CancellationToken cancellationToken)
    {
        while (_pendingCount > 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessInBackground(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IServiceProvider currentServiceProvider, IDictionary<string, object> consumerContextProperties, CancellationToken cancellationToken)
    {
        try
        {
            LogEntering(typeof(TMessage));

            var r = await _target.ProcessMessage(transportMessage, messageHeaders, consumerContextProperties, currentServiceProvider, cancellationToken).ConfigureAwait(false);
            if (_reportBackgroundExceptions && r.Exception != null && r.Result is not ProcessResult.CanceledState)
            {
                _logger.LogError(r.Exception, "Error processing background message of type {MessageType} on {Path}", typeof(TMessage), r.ConsumerSettings?.Path);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Processing stopped with the consumer.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background processing failed for message type {MessageType}", typeof(TMessage));
        }
        finally
        {
            LogLeaving(typeof(TMessage));
            _concurrentSemaphore?.Release();

            Interlocked.Decrement(ref _pendingCount);
        }
    }

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Debug,
       Message = "Entering ProcessMessages for message {MessageType}")]
    private partial void LogEntering(Type messageType);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Debug,
       Message = "Leaving ProcessMessages for message {MessageType}")]
    private partial void LogLeaving(Type messageType);

    #endregion
}

#if NETSTANDARD2_0

public partial class ConcurrentMessageProcessorDecorator<TMessage>
{
    private partial void LogEntering(Type messageType)
        => _logger.LogDebug("Entering ProcessMessages for message {MessageType}", messageType);

    private partial void LogLeaving(Type messageType)
        => _logger.LogDebug("Leaving ProcessMessages for message {MessageType}", messageType);
}

#endif
