namespace SlimMessageBus.Host.KubeMQ
{
    using SlimMessageBus.Host.Config;

    public static class KubeMQMessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithProviderKubeMQ(this MessageBusBuilder mbb, KubeMQMessageBusSettings kubeMQSettings)
        {
            return mbb.WithProvider(settings => new KubeMQMessageBus(settings, kubeMQSettings));
        }
    }
}