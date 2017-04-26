using System;
using Autofac;
using Common.Logging;

namespace SlimMessageBus.Host.Autofac
{
    public class AutofacDependencyResolver : IDependencyResolver
    {
        private static readonly ILog Log = LogManager.GetLogger<AutofacDependencyResolver>();

        private readonly IContainer _container;

        public AutofacDependencyResolver(IContainer container)
        {
            _container = container;
        }

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            Log.DebugFormat("Resolving type {0}", type);
            var o = _container.Resolve(type);
            Log.DebugFormat("Resolved type {0} to object {1}", type, o);
            return o;
        }

        #endregion
    }
}
