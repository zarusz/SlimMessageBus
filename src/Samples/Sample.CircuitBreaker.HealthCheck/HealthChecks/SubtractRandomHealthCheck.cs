namespace Sample.CircuitBreaker.HealthCheck.HealthChecks;

using Microsoft.Extensions.Logging;

public class SubtractRandomHealthCheck : RandomHealthCheck
{
    public SubtractRandomHealthCheck(ILogger<SubtractRandomHealthCheck> logger) 
        : base(logger)
    {
    }
}
