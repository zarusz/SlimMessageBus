using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.Hybrid.ConsoleApp.ApplicationLayer;
using Sample.Hybrid.ConsoleApp.DomainModel;
using Sample.Hybrid.ConsoleApp.EmailService;
using Sample.Hybrid.ConsoleApp.EmailService.Contract;
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
            services.AddLogging(cfg =>
            {
                cfg.AddConfiguration(Configuration);
                cfg.AddConsole();
            });

            services.AddSingleton<Application>();
            services.AddSingleton(CreateMessageBus);

            services.AddTransient<CustomerChangedEventHandler>();
            services.AddTransient<SmtpEmailService>();
        }

        public IMessageBus CreateMessageBus(IServiceProvider svp)
        {
            var hybridBusSettings = new HybridMessageBusSettings
            {
                // Bus 1
                ["Memory"] = builder =>
                {
                    builder
                        .Produce<CustomerEmailChangedEvent>(x => x.DefaultTopic(x.Settings.MessageType.Name))
                        .Consume<CustomerEmailChangedEvent>(x => x.Topic(x.MessageType.Name).WithConsumer<CustomerChangedEventHandler>())
                        .WithProviderMemory(new MemoryMessageBusSettings { EnableMessageSerialization = false });
                },
                // Bus 2
                ["AzureSB"] = builder =>
                {
                    var serviceBusConnectionString = Secrets.Service.PopulateSecrets(Configuration["Azure:ServiceBus"]);
                    builder
                        .Produce<SendEmailCommand>(x => x.DefaultQueue("test-ping-queue"))
                        .Consume<SendEmailCommand>(x => x.Queue("test-ping-queue").WithConsumer<SmtpEmailService>())
                        .WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString));
                }
            };

            var mbb = MessageBusBuilder.Create()
                .WithDependencyResolver(new LookupDependencyResolver(svp.GetRequiredService)) // DI setup will be shared
                .WithSerializer(new JsonMessageSerializer()) // serialization setup will be shared between bus 1 and 2
                .WithProviderHybrid(hybridBusSettings); 

            // In summary:
            // - The CustomerChangedEvent messages will be going through the SMB Memory provider.
            // - The SendEmailCommand messages will be going through the SMB Azure Service Bus provider.
            // - Each of the bus providers will serialize messages using JSON and use the same DI to resolve consumers/handlers.
            var mb = mbb.Build();
            return mb;
        }
    }
}