namespace SlimMessageBus.Host.Outbox.DbContext;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutboxUsingDbContext<TDbContext>(this MessageBusBuilder mbb, Action<DbContextOutboxSettings> configure)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        mbb.AddOutbox();

        mbb.PostConfigurationActions.Add(services =>
        {
            var settings = new[] { mbb.Settings }.Concat(mbb.Children.Values.Select(x => x.Settings)).ToList();

            services.TryAddSingleton(svp =>
            {
                var settings = new DbContextOutboxSettings();
                configure?.Invoke(settings);
                return settings;
            });
            services.Replace(ServiceDescriptor.Transient<OutboxSettings>(svp => svp.GetRequiredService<DbContextOutboxSettings>()));

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for transaction scope
            foreach (var consumerMessageType in settings
                .SelectMany(x => x.Consumers
                    .SelectMany(c => c.Invokers)
                    .Where(ci => ci.ParentSettings.IsEnabledForMessageType(x, BuilderExtensions.PropertyDbContextTransactionEnabled, BuilderExtensions.PropertyDbContextTransactionFilter, ci.MessageType)))
                .Select(x => x.MessageType))
            {
                var serviceType = typeof(IConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                var implementationType = typeof(DbContextTransactionConsumerInterceptor).MakeGenericType(consumerMessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }

            services.TryAddScoped<IOutboxMessageRepository>(svp =>
            {
                var settings = svp.GetRequiredService<DbContextOutboxSettings>();
                return new DbContextOutboxMessageRepository(
                    svp.GetRequiredService<TDbContext>()
                    /*
                    svp.GetRequiredService<ILogger<DbContextOutboxMessageRepository<TDbContext>>>(),
                    settings,
                    svp.GetRequiredService<SqlOutboxTemplate>(),
                    settings.IdGeneration.GuidGenerator ?? (IGuidGenerator)svp.GetRequiredService(settings.IdGeneration.GuidGeneratorType),
                    svp.GetRequiredService<ICurrentTimeProvider>(),
                    svp.GetRequiredService<IInstanceIdProvider>(),
                    svp.GetRequiredService<SqlConnection>(),
                    svp.GetRequiredService<ISqlTransactionService>()
                    */
                    );
            });
        });
        return mbb;
    }
}

