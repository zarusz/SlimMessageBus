namespace SlimMessageBus.Host.RabbitMQ.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Extension methods for registering RabbitMQ health checks.
/// </summary>
public static class RabbitMqHealthCheckExtensions
{
    /// <summary>
    /// Adds a health check for the RabbitMQ message bus.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. If null, "rabbitmq" will be used.</param>
    /// <param name="failureStatus">The health status that should be reported when the health check fails. If null, <see cref="HealthStatus.Unhealthy"/> will be reported.</param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The health checks builder.</returns>
    public static IHealthChecksBuilder AddRabbitMq(
        this IHealthChecksBuilder builder,
        string name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? "rabbitmq",
            serviceProvider => serviceProvider.GetRequiredService<RabbitMqHealthCheck>(),
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// Adds a health check for a specific RabbitMQ channel instance.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="channelFactory">A factory function to resolve the specific RabbitMQ channel instance.</param>
    /// <param name="name">The health check name. If null, "rabbitmq" will be used.</param>
    /// <param name="failureStatus">The health status that should be reported when the health check fails. If null, <see cref="HealthStatus.Unhealthy"/> will be reported.</param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The health checks builder.</returns>
    public static IHealthChecksBuilder AddRabbitMq(
        this IHealthChecksBuilder builder,
        Func<IServiceProvider, IRabbitMqChannel> channelFactory,
        string name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null,
        TimeSpan? timeout = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? "rabbitmq",
            serviceProvider =>
            {
                var channel = channelFactory(serviceProvider);
                var logger = serviceProvider.GetRequiredService<ILogger<RabbitMqHealthCheck>>();
                return new RabbitMqHealthCheck(channel, logger);
            },
            failureStatus,
            tags,
            timeout));
    }

    /// <summary>
    /// Registers the RabbitMQ health check service in the dependency injection container.
    /// Call this before adding the health check using AddRabbitMq().
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRabbitMqHealthCheck(this IServiceCollection services)
    {
        services.AddTransient<RabbitMqHealthCheck>();
        return services;
    }

    /// <summary>
    /// Registers a specific RabbitMQ health check service in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="channelFactory">A factory function to resolve the specific RabbitMQ channel instance.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRabbitMqHealthCheck(
        this IServiceCollection services,
        Func<IServiceProvider, IRabbitMqChannel> channelFactory)
    {
        services.AddTransient(serviceProvider =>
        {
            var channel = channelFactory(serviceProvider);
            var logger = serviceProvider.GetRequiredService<ILogger<RabbitMqHealthCheck>>();
            return new RabbitMqHealthCheck(channel, logger);
        });
        return services;
    }
}