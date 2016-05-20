using System.Collections.Generic;
using SlimMessageBus.Core;

namespace SlimMessageBus.ServiceLocator
{
    public class ServiceLocatorHandlerResolver : IHandlerResolver
    {
        #region Implementation of IHandlerResolver

        public IEnumerable<IHandles<TEvent>> Resolve<TEvent>()
        {
            var handlers = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetAllInstances<IHandles<TEvent>>();
            return handlers;
        }

        #endregion
    }
}
