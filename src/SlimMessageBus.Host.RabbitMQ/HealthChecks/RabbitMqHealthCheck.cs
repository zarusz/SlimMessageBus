namespace SlimMessageBus.Host.RabbitMQ.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for RabbitMQ message bus to verify connection and channel availability.
/// </summary>
public class RabbitMqHealthCheck(RabbitMqMessageBus messageBus, ILogger<RabbitMqHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();

            // Check if the channel is available
            if (messageBus.Channel == null)
            {
                logger.LogWarning("RabbitMQ health check failed: Channel is not available");
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "RabbitMQ channel is not available. Connection may have failed during initialization.",
                    data: data));
            }

            // Check if the channel is open
            if (!messageBus.Channel.IsOpen)
            {
                logger.LogWarning("RabbitMQ health check failed: Channel is closed");
                data["ChannelNumber"] = messageBus.Channel.ChannelNumber;
                data["CloseReason"] = messageBus.Channel.CloseReason?.ToString() ?? "Unknown";

                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "RabbitMQ channel is closed",
                    data: data));
            }

            // All checks passed - channel is available and open
            data["ChannelNumber"] = messageBus.Channel.ChannelNumber;
            data["ChannelIsOpen"] = messageBus.Channel.IsOpen;

            logger.LogDebug("RabbitMQ health check passed");
            return Task.FromResult(HealthCheckResult.Healthy(
                "RabbitMQ connection and channel are healthy",
                data: data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RabbitMQ health check failed with exception");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "RabbitMQ health check failed with exception",
                ex,
                data: new Dictionary<string, object> { ["Exception"] = ex.Message }));
        }
    }
}