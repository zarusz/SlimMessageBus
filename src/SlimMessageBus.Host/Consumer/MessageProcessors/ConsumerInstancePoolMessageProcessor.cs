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
        private IMessageProcessor<TMessage> strategy;
        
        public AbstractConsumerSettings ConsumerSettings { get; }

        public ConsumerInstancePoolMessageProcessor(ConsumerSettings consumerSettings, MessageBusBase messageBus, Func<TMessage, MessageWithHeaders> messageProvider, Action<object, ConsumerContext> consumerContextInitializer = null)
        {
            ConsumerSettings = consumerSettings;
            var consumerInstanceMessageProcessor = new ConsumerInstanceMessageProcessor<TMessage>(consumerSettings, messageBus, messageProvider, consumerContextInitializer);
            strategy = new ConcurrencyLimittingMessageProcessorDecorator<TMessage>(consumerSettings, messageBus, consumerInstanceMessageProcessor);
        }


        public virtual Task<Exception> ProcessMessage(TMessage msg, IMessageTypeConsumerInvokerSettings consumerInvoker) => strategy.ProcessMessage(msg, consumerInvoker);

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (strategy != null)
            {
                await strategy.DisposeAsync();
            }
        }

        #endregion
    }
}