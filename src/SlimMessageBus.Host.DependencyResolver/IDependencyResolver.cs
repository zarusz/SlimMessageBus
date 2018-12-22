using System;

namespace SlimMessageBus.Host.DependencyResolver
{
    /// <summary>
    /// Responsible for resolving the handlers or consumers.
    /// </summary>
    public interface IDependencyResolver
    {
        /// <summary>
        /// Resolves the message handles or consumers.
        /// </summary>
        /// <returns></returns>
        object Resolve(Type type);
    }
}