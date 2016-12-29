using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.ServiceLocator
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithSubscriberResolverAsServiceLocator(this MessageBusBuilder builder)
        {
            return builder.WithSubscriberResolver(new ServiceLocatorDependencyResolver());
        }
    }
}
