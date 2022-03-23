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
        private readonly ILogger logger;
        private SemaphoreSlim concurrentSemaphore;
        private readonly IMessageProcessor<TMessage> target;
        private Exception lastException;
        private TMessage lastExceptionMessage;
        private readonly object lastExceptionLock = new();

        private int pendingCount;

        public int PendingCount => pendingCount;

        public ConcurrencyIncreasingMessageProcessorDecorator(AbstractConsumerSettings consumerSettings, MessageBusBase messageBus, IMessageProcessor<TMessage> target)
        {
            if (consumerSettings is null) throw new ArgumentNullException(nameof(consumerSettings));
            if (messageBus is null) throw new ArgumentNullException(nameof(messageBus));

            logger = messageBus.LoggerFactory.CreateLogger<ConsumerInstancePoolMessageProcessor<TMessage>>();
            concurrentSemaphore = new SemaphoreSlim(consumerSettings.Instances);
            this.target = target;
        }

        public AbstractConsumerSettings ConsumerSettings => target.ConsumerSettings;

        public async Task<Exception> ProcessMessage(TMessage message, IMessageTypeConsumerInvokerSettings consumerInvoker)
        {
            // Ensure only desired number of messages are being processed concurrently
            await concurrentSemaphore.WaitAsync().ConfigureAwait(false);

            // Check if there was an exception from and earlier message processing
            var e = lastException;
            if (e != null)
            {
                // report the last exception
                lastException = null;
                return e;
            }

            Interlocked.Increment(ref pendingCount);

            // Fire and forget
            _ = ProcessInBackground(message, consumerInvoker);

            // Not exception - we don't know yet
            return null;
        }

        public TMessage GetMessageWithException()
        {
            lock (lastExceptionLock)
            {
                var m = lastExceptionMessage;
                lastExceptionMessage = null;
                return m;
            }
        }

        public async Task WaitAll()
        {
            while(pendingCount > 0)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
        }

        private async Task ProcessInBackground(TMessage message, IMessageTypeConsumerInvokerSettings consumerInvoker)
        {
            try
            {
                logger.LogDebug("Entering ProcessMessages for message {MessageType}", typeof(TMessage));
                var exception = await target.ProcessMessage(message, consumerInvoker).ConfigureAwait(false);
                if (exception != null)
                {
                    lock (lastExceptionLock)
                    {
                        // ensure there was no error before this one, in which case forget about this error (the whole event stream will be rewind back).
                        if (lastException == null && lastExceptionMessage == null)
                        {
                            lastException = exception;
                            lastExceptionMessage = message;
                        }
                    }
                }
            }
            finally
            {
                logger.LogDebug("Leaving ProcessMessages for message {MessageType}", typeof(TMessage));
                concurrentSemaphore.Release();

                Interlocked.Decrement(ref pendingCount);
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
            if (concurrentSemaphore != null)
            {
                concurrentSemaphore.Dispose();
                concurrentSemaphore = null;
            }
            await target.DisposeAsync();
        }

        #endregion
    }
}