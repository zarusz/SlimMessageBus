namespace SlimMessageBus.Host.Outbox.Sql;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host.Interceptor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessageBusOutboxUsingSql<TOutboxRepository>(
        this IServiceCollection services,
        Action<SqlOutboxSettings> configure)
        where TOutboxRepository : class, ISqlOutboxRepository
    {
        services.TryAddSingleton(svp =>
        {
            var settings = new SqlOutboxSettings();
            configure?.Invoke(settings);
            return settings;
        });
        services.TryAddSingleton<OutboxSettings>(svp => svp.GetRequiredService<SqlOutboxSettings>());
        
        services.AddMessageBusOutbox();

        services.Add(ServiceDescriptor.Transient(typeof(IConsumerInterceptor<>), typeof(SqlTransactionConsumerInterceptor<>)));

        services.AddScoped<TOutboxRepository>();
        services.TryAddScoped<ISqlOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>());
        services.TryAddScoped<IOutboxRepository>(svp => svp.GetRequiredService<TOutboxRepository>());

        return services;
    }

    public static IServiceCollection AddMessageBusOutboxUsingSql(
        this IServiceCollection services,
        Action<SqlOutboxSettings> configure) =>
        services.AddMessageBusOutboxUsingSql<SqlOutboxRepository>(configure);
}
