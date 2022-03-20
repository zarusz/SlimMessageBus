namespace SlimMessageBus.Host.MsDependencyInjection
{
    using SlimMessageBus.Host.DependencyResolver;
    using System;

    public static class MessageBusCurrentProviderBuilderExtensions
    {
        public static MessageBusCurrentProviderBuilder From(this MessageBusCurrentProviderBuilder builder, IServiceProvider svp)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var dr = new MsDependencyInjectionDependencyResolver(svp);
            return builder.From(dr);
        }
    }
}