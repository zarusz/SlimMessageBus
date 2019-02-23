using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Redis
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderRedis(this MessageBusBuilder mbb, RedisMessageBusSettings redisSettings)
        {
            return mbb.WithProvider(settings => new RedisMessageBus(settings, redisSettings));
        }
    }
}