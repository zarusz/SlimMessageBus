using System;
using Common.Logging;

namespace SlimMessageBus.Host.ServiceLocator
{
    public class ServiceLocatorMessageBusDependencyResolver : IDependencyResolver
    {
        private static readonly ILog Log = LogManager.GetLogger<ServiceLocatorMessageBusDependencyResolver>();
        
        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            Log.DebugFormat("Resolving type {0}", type);
            var o = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance(type);
            Log.DebugFormat("Resolved type {0} to object {1}", type, o);
            return o;
        }

        #endregion
    }
}

