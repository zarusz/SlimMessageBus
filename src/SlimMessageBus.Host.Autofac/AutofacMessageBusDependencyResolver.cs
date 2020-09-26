using System;
using Autofac;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.DependencyResolver;

namespace SlimMessageBus.Host.Autofac
{
    public class AutofacMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly ILogger _logger;

        public AutofacMessageBusDependencyResolver(ILogger<AutofacMessageBusDependencyResolver> logger)
        {
            _logger = logger;
        }

        public static IComponentContext Container { get; set; }

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
