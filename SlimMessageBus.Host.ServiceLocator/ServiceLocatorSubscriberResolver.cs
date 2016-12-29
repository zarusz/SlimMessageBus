using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host.ServiceLocator
{
    public class ServiceLocatorDependencyResolver : IDependencyResolver
    {
        #region Implementation of IDependencyResolver

        public IEnumerable<object> Resolve(Type type)
        {
            var handlers = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetAllInstances(type);
            return handlers;
        }

        #endregion
    }

    
}

