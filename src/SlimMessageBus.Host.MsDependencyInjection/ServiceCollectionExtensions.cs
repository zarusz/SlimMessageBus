namespace SlimMessageBus.Host.MsDependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;
    using System;

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers SlimMessageBus (<see cref="IMessageBus">) singleton instance and configures the ASP.NET Ms Dependency Injection for DI plugin.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configure"></param>
        /// <param name="loggerFactory">Use a custom logger factory. If not provided it will be obtained from the DI.</param>
        /// <param name="configureDependencyResolver">Confgure the DI plugin on the <see cref="MessageBusBuilder"/>. Default is true.</param>
        /// <returns></returns>
        public static IServiceCollection AddSlimMessageBus(
            this IServiceCollection services,
            Action<MessageBusBuilder, IServiceProvider> configure,
            ILoggerFactory loggerFactory = null,
            bool configureDependencyResolver = true)
        {
            services.AddSingleton((svp) =>
            {
                var mbb = MessageBusBuilder.Create();
                if (configureDependencyResolver)
                {
                    mbb.WithDependencyResolver(new MsDependencyInjectionDependencyResolver(svp, loggerFactory));
                }
                configure(mbb, svp);
                return mbb.Build();
            });
            return services;
        }
    }
}
