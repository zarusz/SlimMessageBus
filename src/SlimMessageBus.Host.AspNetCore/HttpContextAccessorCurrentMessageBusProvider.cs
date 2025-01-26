namespace SlimMessageBus.Host;

/// <summary>
/// Resolves the <see cref="IMessageBus"/> from the current ASP.NET Core web request (if present, otherwise falls back to the application root container).
/// </summary>
public partial class HttpContextAccessorCurrentMessageBusProvider(
    ILogger<HttpContextAccessorCurrentMessageBusProvider> logger,
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider serviceProvider)
    : CurrentMessageBusProvider(serviceProvider)
{
    private readonly ILogger<HttpContextAccessorCurrentMessageBusProvider> _logger = logger;

    public override IMessageBus GetCurrent()
    {
        // When the call to resolve the given type is made within an HTTP Request, use the request scope service provider
        var httpContext = httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            LogCurrentFrom("request");
            return httpContext.RequestServices.GetService<IMessageBus>();
        }

        // otherwise use the app wide scope provider
        LogCurrentFrom("root");
        return base.GetCurrent();
    }

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Trace,
       Message = "The type IMessageBus will be requested from the {ScopeName} scope")]
    private partial void LogCurrentFrom(string scopeName);

    #endregion
}

#if NETSTANDARD2_0

public partial class HttpContextAccessorCurrentMessageBusProvider
{
    private partial void LogCurrentFrom(string scopeName)
        => _logger.LogTrace("The type IMessageBus will be requested from the {ScopeName} scope", scopeName);
}

#endif