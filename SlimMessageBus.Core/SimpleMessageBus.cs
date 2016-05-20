using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;

namespace SlimMessageBus.Core
{
    /// <summary>
    /// Simple <see cref="IMessageBus"/> implementation that maintains a list of subscriptions.
    /// This class is not thread-safe.
    /// </summary>
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

        private readonly IDictionary<Type, ICollection<object>> _subscriptions =
            new Dictionary<Type, ICollection<object>>();

        private ICollection<object> GetOwnHandlersOrNull<TEvent>()
        {
            var eventType = typeof(TEvent);
            ICollection<object> handlers;
            _subscriptions.TryGetValue(eventType, out handlers);
            return handlers;
        }

        protected virtual IEnumerable<IHandles<TMessage>> GetOwnTypedHandlersOrNull<TMessage>()
        {
            var handlers = GetOwnHandlersOrNull<TMessage>();
            var typedHandlers = handlers?.Cast<IHandles<TMessage>>();
            return typedHandlers;
        }

        protected virtual IEnumerable<IHandles<TMessage>> GetTypedHandlersOrNull<TMessage>()
        {
            var typedHandlers = GetOwnTypedHandlersOrNull<TMessage>();

            var additionalTypedHandlers = HandlerResolver?.Resolve<TMessage>();
            if (additionalTypedHandlers != null)
            {
                typedHandlers = typedHandlers?.Concat(additionalTypedHandlers) ?? additionalTypedHandlers;
            }

            return typedHandlers;
        }

        #region Implementation of IEventBus

        public void Subscribe<TEvent>(IHandles<TEvent> handler)
        {
            _log.Debug(m => m("Subscribing handler {0} to event {1}", handler, typeof(TEvent)));

            var eventHandlers = GetOwnHandlersOrNull<TEvent>();
            if (eventHandlers == null)
            {
                eventHandlers = new List<object>();
                var eventType = typeof(TEvent);
                _subscriptions.Add(eventType, eventHandlers);
            }

            eventHandlers.Add(handler);
        }

        public void UnSubscribe<TEvent>(IHandles<TEvent> handler)
        {
            _log.Debug(m => m("UnSubscribing handler {0} from event {1}", handler, typeof(TEvent)));

            var removed = false;

            var eventHandlers = GetOwnHandlersOrNull<TEvent>();
            if (eventHandlers != null)
            {
                removed = eventHandlers.Remove(handler);
            }

            if (!removed)
            {
                _log.Warn("An attempt to unsubscribe a handler was not subscribed was made. Check your logic if every UnSubscribe matches exactly one Subscribe call.");
            }
        }

        public void Publish<TEvent>(TEvent e)
        {
            var eventHandlers = GetTypedHandlersOrNull<TEvent>();
            if (eventHandlers != null)
            {
                CallHandlers(eventHandlers, e);
            }
            else
            {
                _log.Debug(m => m("No handler was subscribed for {0} event.", typeof(TEvent)));
            }
        }

        #endregion

        protected virtual void CallHandlers<TEvent>(IEnumerable<IHandles<TEvent>> handlers, TEvent e)
        {
            foreach (var handler in handlers)
            {
                CallHandler(handler, e);
            }
        }

        protected virtual void CallHandler<TEvent>(IHandles<TEvent> handler, TEvent e)
        {
            handler.Handle(e);
        }
    }
}
