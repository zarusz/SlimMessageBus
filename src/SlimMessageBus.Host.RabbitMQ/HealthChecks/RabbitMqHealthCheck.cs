namespace SlimMessageBus.Host.RabbitMQ.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for RabbitMQ message bus to verify connection and channel availability.
/// </summary>
public class RabbitMqHealthCheck(IRabbitMqChannel rabbitMqChannel, ILogger<RabbitMqHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();

            // Check if the channel is available
            if (rabbitMqChannel.Channel == null)
            {
                logger.LogWarning("RabbitMQ health check failed: Channel is not available");
                data["ChannelAvailable"] = false;
                data["ConnectionStatus"] = "Unknown - Channel is null";

                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "RabbitMQ channel is not available. Connection may have failed during initialization or is being retried.",
                    data: data));
            }

            // Channel is available, gather diagnostic information
            data["ChannelAvailable"] = true;
            data["ChannelNumber"] = rabbitMqChannel.Channel.ChannelNumber;

            // Check if the channel is open
            if (!rabbitMqChannel.Channel.IsOpen)
            {
                logger.LogWarning("RabbitMQ health check failed: Channel is closed");
                data["ChannelIsOpen"] = false;
                data["CloseReason"] = rabbitMqChannel.Channel.CloseReason?.ToString() ?? "Unknown";

                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "RabbitMQ channel is closed. Connection retry may be in progress.",
                    data: data));
            }

            // All checks passed - channel is available and open
            data["ChannelIsOpen"] = true;
            data["ConnectionStatus"] = "Connected";

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