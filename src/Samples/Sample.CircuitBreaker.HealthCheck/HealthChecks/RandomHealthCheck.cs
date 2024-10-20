namespace Sample.CircuitBreaker.HealthCheck.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

public abstract class RandomHealthCheck : IHealthCheck
{
    private readonly ILogger _logger;

    protected RandomHealthCheck(ILogger logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var value = (HealthStatus)Random.Shared.Next(3);
        _logger.LogInformation("{HealthCheck} evaluated as {HealthStatus}", this.GetType(), value);
        return Task.FromResult(new HealthCheckResult(value, value.ToString()));
    }
}
