namespace Sample.CircuitBreaker.HealthCheck.HealthChecks;

public class AddRandomHealthCheck(ILogger<AddRandomHealthCheck> logger) : RandomHealthCheck(logger)
{
}
