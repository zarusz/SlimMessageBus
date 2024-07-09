namespace SlimMessageBus.Host;

using SlimMessageBus;

/// <summary>
/// Resolves the <see cref="IMessageBus"/> from the current ASP.NET Core web request (if present, otherwise falls back to the application root container).
/// </summary>
internal class HttpContextAccessorCurrentMessageBusProvider : CurrentMessageBusProvider
{
    private readonly ILogger _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAccessorCurrentMessageBusProvider(IServiceProvider serviceProvider, IHttpContextAccessor httpContextAccessor)
        : base(serviceProvider)
    {
        _logger = serviceProvider.GetService<ILogger<HttpContextAccessorCurrentMessageBusProvider>>() ?? NullLogger<HttpContextAccessorCurrentMessageBusProvider>.Instance;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IMessageBus GetCurrent()
    {
        // When the call to resolve the given type is made within an HTTP Request, use the request scope service provider
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            _logger.LogTrace("The type IMessageBus will be requested from the per-request scope");
            return httpContext.RequestServices.GetService<IMessageBus>();
        }

        // otherwise use the app wide scope provider
        _logger.LogTrace("The type IMessageBus will be requested from the app scope");
        return base.GetCurrent();
    }
}
