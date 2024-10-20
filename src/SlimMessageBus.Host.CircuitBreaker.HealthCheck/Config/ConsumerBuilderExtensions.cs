namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck.Config;

using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ConsumerBuilderExtensions
{
    public static T PauseOnUnhealthyCheck<T>(this T builder, params string[] tags)
        where T : AbstractConsumerBuilder
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.ConsumerSettings.PauseOnUnhealthy(tags);
        RegisterHealthServices(builder);
        return builder;
    }

    public static T PauseOnDegradedHealthCheck<T>(this T builder, params string[] tags)
        where T : AbstractConsumerBuilder
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.ConsumerSettings.PauseOnDegraded(tags);
        RegisterHealthServices(builder);
        return builder;
    }

    private static void RegisterHealthServices(AbstractConsumerBuilder builder)
    {
        builder.ConsumerSettings.CircuitBreakers.TryAdd<HealthCheckCircuitBreaker>();
        builder.PostConfigurationActions.Add(
            services =>
            {
                services.TryAddSingleton<HealthCheckBackgroundService>();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHealthCheckPublisher, HealthCheckBackgroundService>(sp => sp.GetRequiredService<HealthCheckBackgroundService>()));
                services.TryAdd(ServiceDescriptor.Singleton<IHealthCheckHostBreaker, HealthCheckBackgroundService>(sp => sp.GetRequiredService<HealthCheckBackgroundService>()));
                services.AddHostedService(sp => sp.GetRequiredService<HealthCheckBackgroundService>());

                services.TryAddSingleton<HealthCheckCircuitBreaker>();
            });
    }
}
