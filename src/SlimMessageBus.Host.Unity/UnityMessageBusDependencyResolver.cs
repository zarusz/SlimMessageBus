using System;
using SlimMessageBus.Host.DependencyResolver;
using Unity;
using Microsoft.Extensions.Logging;

namespace SlimMessageBus.Host.Unity
{
    public class UnityMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger _logger;

        public UnityMessageBusDependencyResolver(ILogger logger)
        {
            _logger = logger;
        }

        public static IUnityContainer Container { get; set; }

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            if (Container == null)
            {
                throw new ArgumentNullException($"The {nameof(Container)} property was null at this point");
            }

            _logger.LogTrace("Resolving type {type}", type);
            var o = Container.Resolve(type);
            _logger.LogTrace("Resolved type {type} to instance {instance}", type, o);
            return o;
        }

        #endregion
    }
}
