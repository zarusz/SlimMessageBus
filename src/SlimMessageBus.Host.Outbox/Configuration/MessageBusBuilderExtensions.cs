namespace SlimMessageBus.Host.Outbox;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutbox(this MessageBusBuilder mbb, Action<OutboxSettings> configure = null)
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            var settings = new[] { mbb.Settings }.Concat(mbb.Children.Values.Select(x => x.Settings)).ToList();

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for outbox
            foreach (var producerMessageType in settings
                .SelectMany(x => x.Producers.Where(producerSettings => producerSettings.IsOutboxEnabled(x)))
                .Select(x => x.MessageType))
            {
                var serviceType = typeof(IPublishInterceptor<>).MakeGenericType(producerMessageType);
                var implementationType = typeof(OutboxForwardingPublishInterceptor<>).MakeGenericType(producerMessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }
            // Without optimization: services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPublishInterceptor<>), typeof(OutboxForwardingPublishInterceptor<>)));

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for transaction scope
            foreach (var consumerMessageType in settings
                .SelectMany(x => x.Consumers.SelectMany(x => x.Invokers).Where(consumerInvoker => consumerInvoker.ParentSettings.IsTransactionScopeEnabled(x)))
                .Select(x => x.MessageType))
            {
                var serviceType = typeof(IConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                var implementationType = typeof(TransactionScopeConsumerInterceptor<>).MakeGenericType(consumerMessageType);
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
