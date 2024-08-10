namespace SlimMessageBus.Host;

/// <summary>
/// Resolves the <see cref="IMessageBus"/> from the current ASP.NET Core web request (if present, otherwise falls back to the application root container).
/// </summary>
public class HttpContextAccessorCurrentMessageBusProvider(
    ILogger<HttpContextAccessorCurrentMessageBusProvider> logger,
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider serviceProvider)
    : CurrentMessageBusProvider(serviceProvider)
{
    public override IMessageBus GetCurrent()
    {
        // When the call to resolve the given type is made within an HTTP Request, use the request scope service provider
        var httpContext = httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            logger.LogTrace("The type IMessageBus will be requested from the per-request scope");
            return httpContext.RequestServices.GetService<IMessageBus>();
        }

        // otherwise use the app wide scope provider
        logger.LogTrace("The type IMessageBus will be requested from the app scope");
        return base.GetCurrent();
    }
}
