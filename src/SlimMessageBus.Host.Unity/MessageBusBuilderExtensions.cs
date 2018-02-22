using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Unity
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithDependencyResolverAsUnity(this MessageBusBuilder builder)
        {
            return builder.WithDependencyResolver(new UnityMessageBusDependencyResolver());
        }
    }
}
