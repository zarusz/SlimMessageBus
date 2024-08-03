namespace Sample.CircuitBreaker.HealthCheck.HealthChecks;

using Microsoft.Extensions.Logging;

public class AddRandomHealthCheck : RandomHealthCheck
{
    public AddRandomHealthCheck(ILogger<AddRandomHealthCheck> logger) 
        : base(logger)
    {
    }
}
