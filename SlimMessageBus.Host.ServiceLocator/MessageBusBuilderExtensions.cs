using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.ServiceLocator
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithDependencyResolverAsServiceLocator(this MessageBusBuilder builder)
        {
            return builder.WithDependencyResolver(new ServiceLocatorMessageBusDependencyResolver());
        }
    }
}
