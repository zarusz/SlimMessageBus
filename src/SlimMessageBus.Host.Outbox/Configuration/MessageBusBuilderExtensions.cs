﻿namespace SlimMessageBus.Host.Outbox;

using SlimMessageBus.Host.Outbox.Services;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutbox<TOutboxMessage, TOutboxMessageKey>(this MessageBusBuilder mbb, Action<OutboxSettings> configure = null)
        where TOutboxMessage : OutboxMessage<TOutboxMessageKey>
    {
        mbb.PostConfigurationActions.Add(services =>
        {
            var settings = new[] { mbb.Settings }.Concat(mbb.Children.Values.Select(x => x.Settings)).ToList();

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for outbox
            foreach (var producerMessageType in settings
                .SelectMany(x => x.Producers
                    .Where(producerSettings => producerSettings.IsEnabledForMessageType(x, BuilderExtensions.PropertyOutboxEnabled, BuilderExtensions.PropertyOutboxFilter, producerSettings.MessageType)))
                .Select(x => x.MessageType))
            {
                var serviceType = typeof(IPublishInterceptor<>).MakeGenericType(producerMessageType);
                var implementationType = typeof(OutboxForwardingPublishInterceptor<>).MakeGenericType(producerMessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }

            // Optimization: only register generic interceptors in the DI for particular message types that have opted in for transaction scope
            foreach (var consumerMessageType in settings
                .SelectMany(x => x.Consumers
                    .SelectMany(c => c.Invokers)
                    .Where(ci => ci.ParentSettings.IsEnabledForMessageType(x, BuilderExtensions.PropertyTransactionScopeEnabled, BuilderExtensions.PropertyTransactionScopeFilter, ci.MessageType)))
                .Select(x => x.MessageType))
            {
                var serviceType = typeof(IConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                var implementationType = typeof(TransactionScopeConsumerInterceptor<>).MakeGenericType(consumerMessageType);
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implementationType));
            }

            services.AddSingleton<OutboxSendingTask<TOutboxMessage, TOutboxMessageKey>>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageBusLifecycleInterceptor, OutboxSendingTask<TOutboxMessage, TOutboxMessageKey>>(sp => sp.GetRequiredService<OutboxSendingTask<TOutboxMessage, TOutboxMessageKey>>()));
            services.TryAddSingleton<IOutboxNotificationService>(sp => sp.GetRequiredService<OutboxSendingTask<TOutboxMessage, TOutboxMessageKey>>());

            services.TryAddSingleton<IInstanceIdProvider, DefaultInstanceIdProvider>();
            services.TryAddSingleton<IOutboxLockRenewalTimerFactory, OutboxLockRenewalTimerFactory<TOutboxMessage, TOutboxMessageKey>>();

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
