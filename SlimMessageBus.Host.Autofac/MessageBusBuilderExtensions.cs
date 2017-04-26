using Autofac;
using SlimMessageBus.Host.Autofac;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.ServiceLocator
{
    public static class MessageBusBuilderExtensions
    {
        public static MessageBusBuilder WithDependencyResolverAsAutofac(this MessageBusBuilder builder, IContainer container)
        {
            return builder.WithDependencyResolver(new AutofacDependencyResolver(container));
        }
    }
}
