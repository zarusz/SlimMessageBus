namespace SlimMessageBus.Host.Outbox;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder AddOutbox(this MessageBusBuilder mbb, Action<OutboxSettings> configure = null)
    {
        if (mbb.Services is null)
        {
            return mbb;
        }

        mbb.Services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPublishInterceptor<>), typeof(OutboxForwardingPublishInterceptor<>)));
        mbb.Services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IConsumerInterceptor<>), typeof(TransactionScopeConsumerInterceptor<>)));

        mbb.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageBusLifecycleInterceptor, OutboxSendingTask>());
        mbb.Services.TryAddSingleton<IInstanceIdProvider, DefaultInstanceIdProvider>();

        mbb.Services.TryAddSingleton(svp =>
        {
            var settings = new OutboxSettings();
            configure?.Invoke(settings);
            return settings;
        });

        return mbb;
    }
}
