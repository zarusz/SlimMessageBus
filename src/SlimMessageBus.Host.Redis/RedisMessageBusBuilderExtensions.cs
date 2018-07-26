using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Redis
{

    public static class RedisMessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderRedis(this MessageBusBuilder mbb, RedisMessageBusSettings kafkaSettings)
        {
            return mbb.WithProvider(settings => new RedisMessageBus(settings, kafkaSettings));
        }
    }
}
