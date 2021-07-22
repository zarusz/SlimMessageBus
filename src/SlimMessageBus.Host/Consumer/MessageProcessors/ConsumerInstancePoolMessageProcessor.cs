namespace SlimMessageBus.Host
{
    using System;
    using System.Threading.Tasks;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// Represents a set of consumer instances that compete to process a message. 
    /// Instances are obtained from <see cref="IDependencyResolver"/> upon message arrival.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public class ConsumerInstancePoolMessageProcessor<TMessage> : IMessageProcessor<TMessage> where TMessage : class
    {
        private readonly IMessageProcessor<TMessage> _strategy;

        public ConsumerInstancePoolMessageProcessor(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, MessageWithHeaders> messageProvider, Action<TMessage, ConsumerContext> consumerContextInitializer = null)
        {
            var consumerInstanceMessageProcessor = new ConsumerInstanceMessageProcessor<TMessage>(consumerSettings, messageBus, messageProvider, consumerContextInitializer);
            _strategy = new ConcurrencyLimittingMessageProcessorDecorator<TMessage>(consumerSettings, messageBus, consumerInstanceMessageProcessor);
        }

        public virtual Task<Exception> ProcessMessage(TMessage msg) => _strategy.ProcessMessage(msg);

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
                _strategy.Dispose();
            }
        }

        #endregion
    }
}