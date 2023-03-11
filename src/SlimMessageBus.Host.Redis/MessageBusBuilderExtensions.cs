namespace SlimMessageBus.Host.Redis;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderRedis(this MessageBusBuilder mbb, RedisMessageBusSettings redisSettings)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        return mbb.WithProvider(settings => new RedisMessageBus(settings, redisSettings));
    }
}