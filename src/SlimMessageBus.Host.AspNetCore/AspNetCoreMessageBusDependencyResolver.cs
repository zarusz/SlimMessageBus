using System;
using Microsoft.AspNetCore.Http;
using SlimMessageBus.Host.DependencyResolver;

namespace SlimMessageBus.Host.AspNetCore
{
    /// <summary>
    /// <see cref="IDependencyResolver"/> implementation that resolves dependencies from the current ASP.NET Core web request.
    /// </summary>
    public class AspNetCoreMessageBusDependencyResolver : IDependencyResolver
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AspNetCoreMessageBusDependencyResolver(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        #region Implementation of IDependencyResolver

        public object Resolve(Type type)
        {
            return _httpContextAccessor.HttpContext.RequestServices.GetService(type);
        }

        #endregion
    }
}
