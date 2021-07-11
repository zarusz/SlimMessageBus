namespace SlimMessageBus.Host
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// Decorator for <see cref="IMessageProcessor{TMessage}"> that limits the amount of messages being concurrently processed.
    /// The expectation is that <see cref="IMessageProcessor{TMessage}.ProcessMessage(TMessage)"/> will be executed concurrently by the caller on which we need to limit the amount of concurrent method executions.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConcurrencyLimittingMessageProcessorDecorator<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _concurrentSemaphore;
        private readonly IMessageProcessor<TMessage> _target;

        public ConcurrencyLimittingMessageProcessorDecorator(AbstractConsumerSettings consumerSettings, MessageBusBase messageBus, IMessageProcessor<TMessage> target)
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
            try
            {
                _logger.LogDebug("Entering ProcessMessages for message {MessageType}", typeof(TMessage));
                return await _target.ProcessMessage(message).ConfigureAwait(false);
            }
            finally
            {
                _logger.LogDebug("Leaving ProcessMessages for message {MessageType}", typeof(TMessage));
                _concurrentSemaphore.Release();
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