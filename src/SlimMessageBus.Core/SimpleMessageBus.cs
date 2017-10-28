using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;

namespace SlimMessageBus.Core
{

    /// <summary>
    /// SimpleMessageBus <see cref="IMessageBus"/> implementation that maintains a list of subscriptions.
    /// This class is not thread-safe.
    /// </summary>
    /*
    public class SimpleMessageBus : IMessageBus
    {
        private readonly ILog _log;
        public IHandlerResolver HandlerResolver { get; set; }

        protected SimpleMessageBus(ILog log)
        {
            _log = log;
        }

        public SimpleMessageBus()
            : this(LogManager.GetLogger<SimpleMessageBus>())
        {
        }

        private readonly IDictionary<Type, ICollection<object>> _subscriptions = new Dictionary<Type, ICollection<object>>();

        private ICollection<object> GetOwnHandlersOrNull<TEvent>()
        {
            var eventType = typeof(TEvent);
            ICollection<object> handlers;
            _subscriptions.TryGetValue(eventType, out handlers);
            return handlers;
        }

        protected virtual IEnumerable<IConsumer<TMessage>> GetOwnTypedHandlersOrNull<TMessage>()
        {
            var handlers = GetOwnHandlersOrNull<TMessage>();
            var typedHandlers = handlers?.Cast<IConsumer<TMessage>>();
            return typedHandlers;
        }

        protected virtual IEnumerable<IConsumer<TMessage>> GetTypedHandlersOrNull<TMessage>()
        {
            var typedHandlers = GetOwnTypedHandlersOrNull<TMessage>();

            var additionalTypedHandlers = HandlerResolver?.Resolve<TMessage>();
            if (additionalTypedHandlers != null)
            {
                typedHandlers = typedHandlers?.Concat(additionalTypedHandlers) ?? additionalTypedHandlers;
            }

            return typedHandlers;
        }

        #region Implementation of IMessageBus

        #region Implementation of IDisposable

        public void Dispose()
        {
            
        }

        #endregion


        public void Subscribe<TEvent>(IConsumer<TEvent> handler)
        {
            _log.Debug(m => m("Subscribing handler {0} to event {1}", handler, typeof(TEvent)));

            var handlers = GetOwnHandlersOrNull<TEvent>();
            if (handlers == null)
            {
                handlers = new List<object>();
                var eventType = typeof(TEvent);
                _subscriptions.Add(eventType, handlers);
            }

            handlers.Add(handler);
        }

        public void UnSubscribe<TEvent>(IConsumer<TEvent> handler)
        {
            _log.Debug(m => m("UnSubscribing handler {0} from event {1}", handler, typeof(TEvent)));

            var removed = false;

            var handlers = GetOwnHandlersOrNull<TEvent>();
            if (handlers != null)
            {
                removed = handlers.Remove(handler);
            }

            if (!removed)
            {
                _log.Warn("An attempt to unsubscribe a handler was not subscribed was made. Check your logic if every UnSubscribe matches exactly one Subscribe call.");
            }
        }

        public void Publish<TEvent>(TEvent e)
        {
            var handlers = GetTypedHandlersOrNull<TEvent>();
            if (handlers != null)
            {
                CallHandlers(handlers, e);
            }
            else
            {
                _log.Debug(m => m("No handler was subscribed for {0} event.", typeof(TEvent)));
            }
        }

        #endregion

        protected virtual void CallHandlers<TEvent>(IEnumerable<IConsumer<TEvent>> handlers, TEvent e)
        {
            foreach (var handler in handlers)
            {
                CallHandler(handler, e);
            }
        }

        protected virtual void CallHandler<TEvent>(IConsumer<TEvent> handler, TEvent e)
        {
            handler.Handle(e);
        }
    }
    */
}
