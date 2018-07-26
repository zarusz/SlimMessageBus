using Autofac;
using Common.Logging;
using System;
using System.Globalization;

namespace SlimMessageBus.Host.Autofac
{
    public class AutofacMessageBusDependencyResolver : IDependencyResolver
    {
        private static readonly ILog Log = LogManager.GetLogger<AutofacMessageBusDependencyResolver>();

        public static IComponentContext Container { get; set; }

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            Log.DebugFormat(CultureInfo.InvariantCulture, "Resolving type {0}", type);
            Assert.IsTrue(Container != null, () => new ConfigurationMessageBusException($"The {nameof(Container)} property was null at this point"));
            var o = Container.Resolve(type);
            Log.DebugFormat(CultureInfo.InvariantCulture, "Resolved type {0} to object {1}", type, o);
            return o;
        }

        #endregion
    }
}
