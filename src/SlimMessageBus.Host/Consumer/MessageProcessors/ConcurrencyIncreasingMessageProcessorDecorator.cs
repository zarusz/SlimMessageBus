namespace SlimMessageBus.Host
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// Decorator for <see cref="IMessageProcessor{TMessage}"> that increases the amount of messages being concurrently processed.
    /// The expectation is that <see cref="IMessageProcessor{TMessage}.ProcessMessage(TMessage)"/> will be executed synchronously (in sequential order) by the caller on which we want to increase amount of concurrent message being processed.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConcurrencyIncreasingMessageProcessorDecorator<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _concurrentSemaphore;
        private readonly IMessageProcessor<TMessage> _target;
        private Exception _lastException;
        private int _pendingCount;

        public int PendingCount => _pendingCount;

        public ConcurrencyIncreasingMessageProcessorDecorator(AbstractConsumerSettings consumerSettings, MessageBusBase messageBus, IMessageProcessor<TMessage> target)
        {
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));
            if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

            _logger = messageBus.LoggerFactory.CreateLogger<ConsumerInstancePoolMessageProcessor<TMessage>>();
            _concurrentSemaphore = new SemaphoreSlim(consumerSettings.Instances);
            _target = target;
        }

        public async Task<Exception> ProcessMessage(TMessage message)
        {
            // Ensure only desired number of messages are being processed concurrently
            await _concurrentSemaphore.WaitAsync().ConfigureAwait(false);

            // Check if there was an exception from and earlier message processing
            var e = _lastException;
            if (e != null)
            {
                // report the last exception
                _lastException = null;
                return e;
            }

            Interlocked.Increment(ref _pendingCount);
            // Fire and forget
            _ = ProcessInBackground(message);

            // Not exception - we don't know yet
            return null;
        }

        private async Task ProcessInBackground(TMessage message)
        {
            try
            {
                _logger.LogDebug("Entering ProcessMessages for message {MessageType}", typeof(TMessage));
                var exception = await _target.ProcessMessage(message).ConfigureAwait(false);
                if (exception != null)
                {
                    _lastException = exception;
                }
            }
            finally
            {
                _logger.LogDebug("Leaving ProcessMessages for message {MessageType}", typeof(TMessage));
                _concurrentSemaphore.Release();

                Interlocked.Decrement(ref _pendingCount);
            }
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
                _concurrentSemaphore.Dispose();
                _target.Dispose();
            }
        }

        #endregion
    }
}