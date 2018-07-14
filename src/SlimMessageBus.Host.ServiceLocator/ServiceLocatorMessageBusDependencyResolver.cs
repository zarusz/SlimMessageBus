using System;
using System.Globalization;
using Common.Logging;

namespace SlimMessageBus.Host.ServiceLocator
{
    public class ServiceLocatorMessageBusDependencyResolver : IDependencyResolver
    {
        private static readonly ILog Log = LogManager.GetLogger<ServiceLocatorMessageBusDependencyResolver>();

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            Log.DebugFormat(CultureInfo.InvariantCulture, "Resolving type {0}", type);
            var o = CommonServiceLocator.ServiceLocator.Current.GetInstance(type);
            Log.DebugFormat(CultureInfo.InvariantCulture, "Resolved type {0} to object {1}", type, o);
            return o;
        }

        #endregion
    }
}

