namespace Sample.CircuitBreaker.HealthCheck.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

public abstract class RandomHealthCheck(ILogger logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var value = (HealthStatus)Random.Shared.Next(3);
        logger.LogInformation("{HealthCheck} evaluated as {HealthStatus}", GetType(), value);
        return Task.FromResult(new HealthCheckResult(value, value.ToString()));
    }
}
