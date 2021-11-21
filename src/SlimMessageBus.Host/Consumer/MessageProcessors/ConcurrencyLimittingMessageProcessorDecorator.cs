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
        private readonly ILogger logger;
        private readonly SemaphoreSlim concurrentSemaphore;
        private readonly IMessageProcessor<TMessage> target;

        public ConcurrencyLimittingMessageProcessorDecorator(AbstractConsumerSettings consumerSettings, MessageBusBase messageBus, IMessageProcessor<TMessage> target)
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
            try
            {
                logger.LogDebug("Entering ProcessMessages for message {MessageType}", typeof(TMessage));
                return await target.ProcessMessage(message, consumerInvoker).ConfigureAwait(false);
            }
            finally
            {
                logger.LogDebug("Leaving ProcessMessages for message {MessageType}", typeof(TMessage));
                concurrentSemaphore.Release();
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
                concurrentSemaphore.Dispose();
                target.Dispose();
            }
        }

        #endregion
    }
}