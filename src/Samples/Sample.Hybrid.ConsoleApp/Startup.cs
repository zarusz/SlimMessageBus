using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecretStore;
using SlimMessageBus;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Hybrid;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.Json;

namespace Sample.Hybrid.ConsoleApp
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(IConfigurationRoot configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton<Application>();
            services.AddSingleton(CreateMessageBus);
        }

        public IMessageBus CreateMessageBus(IServiceProvider svp)
        {
            var hybridSettings = new HybridMessageBusSettings
            {
                ["Memory"] = builder =>
                {
                    builder.WithProviderMemory(new MemoryMessageBusSettings {EnableMessageSerialization = false});
                },
                ["AzureSB"] = builder =>
                {
                    var serviceBusConnectionString = Secrets.Service.PopulateSecrets(Configuration["Azure:ServiceBus"]);
                    builder.WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString));
                }
            };

            var mbb = MessageBusBuilder
                .Create()
                .WithDependencyResolver(new LookupDependencyResolver(svp.GetService))
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderHybrid(hybridSettings);

            var mb = mbb.Build();
            return mb;
        }


    }
}