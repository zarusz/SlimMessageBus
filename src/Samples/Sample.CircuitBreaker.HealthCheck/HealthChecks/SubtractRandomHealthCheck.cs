namespace Sample.CircuitBreaker.HealthCheck.HealthChecks;

public class SubtractRandomHealthCheck(ILogger<SubtractRandomHealthCheck> logger) : RandomHealthCheck(logger)
{
}
