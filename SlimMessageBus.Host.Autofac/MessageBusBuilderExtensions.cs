using SlimMessageBus.Host.Autofac;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.ServiceLocator
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithDependencyResolverAsAutofac(this MessageBusBuilder builder)
        {
            return builder.WithDependencyResolver(new AutofacMessageBusDependencyResolver());
        }
    }
}
