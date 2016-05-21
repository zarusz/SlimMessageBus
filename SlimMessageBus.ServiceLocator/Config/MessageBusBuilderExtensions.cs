using SlimMessageBus.Core.Config;

namespace SlimMessageBus.ServiceLocator.Config
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder ResolveHandlersFromServiceLocator(this MessageBusBuilder builder)
        {
            return builder.ResolveHandlersFrom(new ServiceLocatorHandlerResolver());
        }
    }
}
