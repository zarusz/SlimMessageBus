using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Microsoft.Practices.ServiceLocation;

namespace Infusion.Voyager.Common.EventBus.Impl
{
    public class LocatorResolvingEventBus : SimpleEventBus
    {
        public LocatorResolvingEventBus()
            : base(LogManager.GetLogger<LocatorResolvingEventBus>())
        {
        }

        #region Overrides of SimpleMessageBus

        protected override IEnumerable<IEventHandler<TEvent>> GetTypedHandlersOrNull<TEvent>()
        {
            var handlersFromSubscriptions = base.GetTypedHandlersOrNull<TEvent>();
            var handlersFromContainer = ServiceLocator.Current.GetAllInstances<IEventHandler<TEvent>>();
            // Concatenate the list of handlers, first the explicit subscriptions (if exist), else the container resolved handlers.
            var handlersCombined = handlersFromSubscriptions?.Concat(handlersFromContainer) ?? handlersFromContainer;
            return handlersCombined;
        }

        #endregion
    }
}
