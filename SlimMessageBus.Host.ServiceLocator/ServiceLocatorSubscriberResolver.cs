using System.Collections.Generic;

namespace SlimMessageBus.Host.ServiceLocator
{
    public class ServiceLocatorSubscriberResolver : ISubscriberResolver
    {
        #region Implementation of ISubscriberResolver

        public IEnumerable<ISubscriber<TMessage>> Resolve<TMessage>()
        {
            var handlers = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetAllInstances<ISubscriber<TMessage>>();
            return handlers;
        }

        #endregion
    }

    
}

