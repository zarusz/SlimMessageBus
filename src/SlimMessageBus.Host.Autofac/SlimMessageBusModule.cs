namespace SlimMessageBus.Host
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Autofac;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;

    /// <summary>
    /// Autofac module for configuring dependencies of SlimMessageBus
    /// </summary>
    public class SlimMessageBusModule : Autofac.Module
    {
        public Action<MessageBusBuilder, IComponentContext> ConfigureBus { get; set; }
        /// <summary>
        /// When set, finds consumers witin the the specified assemblies and registers them (InstancePerDependency) within the container.
        /// </summary>
        public Assembly[] AddConsumersFromAssembly { get; set; }
        /// <summary>
        /// When set, finds configurators <see cref="IMessageBusConfigurator"/> witin the the specified assemblies and registers them (InstancePerDependency) within the container.
        /// </summary>
        public Assembly[] AddConfiguratorsFromAssembly { get; set; }
        /// <summary>
        /// When set, finds interceptos witin the the specified assemblies and registers them (InstancePerDependency) within the container.
        /// </summary>
        public Assembly[] AddInterceptorsFromAssembly { get; set; }
        /// <summary>
        /// When set, will be used as the logger factory, otherwise SMB will try to resolve from container.
        /// </summary>
        public ILoggerFactory LoggerFactory { get; set; }

        protected override void Load(ContainerBuilder builder)
        {
            if (ConfigureBus is null) throw new ArgumentNullException(nameof(ConfigureBus), $"Ensure the {nameof(ConfigureBus)} is set. It is needed to decribe how to configure SlimMessageBus");

            if (AddConsumersFromAssembly != null)
            {
                foreach (var foundType in ReflectionDiscoveryScanner.From(AddConsumersFromAssembly).GetConsumerTypes())
                {
                    builder.RegisterType(foundType.ConsumerType);
                }
            }

            if (AddConfiguratorsFromAssembly != null)
            {
                foreach (var foundType in ReflectionDiscoveryScanner.From(AddConfiguratorsFromAssembly).GetMessageBusConfiguratorTypes())
                {
                    builder.RegisterType(foundType).As<IMessageBusConfigurator>();
                }
            }

            if (AddInterceptorsFromAssembly != null)
            {
                foreach (var foundType in ReflectionDiscoveryScanner.From(AddInterceptorsFromAssembly).GetInterceptorTypes())
                {
                    builder.RegisterType(foundType.Type).As(foundType.InterfaceType);
                }
            }

            builder.RegisterType<AutofacMessageBusDependencyResolver>().As<IDependencyResolver>();

            // Single master bus that holds the defined consumers and message processing pipelines
            builder.Register((ctx) =>
                {
                    var mbb = MessageBusBuilder.Create();
                    mbb.WithDependencyResolver(ctx.Resolve<IDependencyResolver>());
                    mbb.Configurators = ctx.Resolve<IEnumerable<IMessageBusConfigurator>>();

                    ConfigureBus(mbb, ctx);

                    return (IMasterMessageBus)mbb.Build();
                })
                .As<IMasterMessageBus>()
                .SingleInstance();

            builder.Register<IConsumerControl>(ctx => ctx.Resolve<IMasterMessageBus>());

            // Register transient message bus - this is a lightweight proxy that just introduces the current DI scope
            builder.Register(ctx => new MessageBusProxy(ctx.Resolve<IMasterMessageBus>(), ctx.Resolve<IDependencyResolver>()));

            builder.Register<IMessageBus>(ctx => ctx.Resolve<MessageBusProxy>());
            builder.Register<IPublishBus>(ctx => ctx.Resolve<MessageBusProxy>());
            builder.Register<IRequestResponseBus>(ctx => ctx.Resolve<MessageBusProxy>());
        }
    }
}
