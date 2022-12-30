namespace SlimMessageBus.Host.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host.Interceptor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessageBusOutbox(this IServiceCollection services, Action<OutboxSettings> configure = null)
    {
        services.Add(ServiceDescriptor.Transient(typeof(IPublishInterceptor<>), typeof(OutboxForwardingPublishInterceptor<>)));
        services.Add(ServiceDescriptor.Transient(typeof(IConsumerInterceptor<>), typeof(TransactionScopeConsumerInterceptor<>)));
        
        services.AddSingleton<IMessageBusLifecycleInterceptor, OutboxSendingTask>();
        services.TryAddSingleton<IInstanceIdProvider, DefaultInstanceIdProvider>();
        
        services.TryAddSingleton(svp =>
        {
            var settings = new OutboxSettings();
            configure?.Invoke(settings);
            return settings;
        });

        return services;
    }
}
