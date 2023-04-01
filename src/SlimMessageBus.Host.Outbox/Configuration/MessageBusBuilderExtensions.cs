namespace SlimMessageBus.Host.Outbox;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutbox(this MessageBusBuilder mbb, Action<OutboxSettings> configure = null)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            var settings = new[] { mbb.Settings }.Concat(mbb.Children.Values.Select(x => x.Settings)).ToList();

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for outbox
            foreach (var producerSettings in settings.SelectMany(x => x.Producers.Where(producerSettings => producerSettings.IsOutboxEnabled(x))))
            {
                var serviceType = typeof(IPublishInterceptor<>).MakeGenericType(producerSettings.MessageType);
                var implementationType = typeof(OutboxForwardingPublishInterceptor<>).MakeGenericType(producerSettings.MessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }
            // Without optimization: services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPublishInterceptor<>), typeof(OutboxForwardingPublishInterceptor<>)));

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for transaction scope
            foreach (var consumerInvoker in settings.SelectMany(x => x.Consumers.SelectMany(x => x.Invokers).Where(consumerInvoker => consumerInvoker.ParentSettings.IsTransactionScopeEnabled(x))))
            {
                var serviceType = typeof(IConsumerInterceptor<>).MakeGenericType(consumerInvoker.MessageType);
                var implementationType = typeof(TransactionScopeConsumerInterceptor<>).MakeGenericType(consumerInvoker.MessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }
            // Without optimization: services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IConsumerInterceptor<>), typeof(TransactionScopeConsumerInterceptor<>)));

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageBusLifecycleInterceptor, OutboxSendingTask>());
            services.TryAddSingleton<IInstanceIdProvider, DefaultInstanceIdProvider>();

            services.TryAddSingleton(svp =>
            {
                var settings = new OutboxSettings();
                configure?.Invoke(settings);
                return settings;
            });
        });
        return mbb;
    }
}
