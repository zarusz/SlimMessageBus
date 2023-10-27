namespace SlimMessageBus.Host.Redis;

public static class RedisMessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderRedis(this MessageBusBuilder mbb, Action<RedisMessageBusSettings> configure)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new RedisMessageBusSettings();
        configure(providerSettings);

        return mbb.WithProvider(settings => new RedisMessageBus(settings, providerSettings));
    }
}