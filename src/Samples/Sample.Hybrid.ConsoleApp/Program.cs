namespace Sample.Hybrid.ConsoleApp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Sample.Hybrid.ConsoleApp.Application;
using Sample.Hybrid.ConsoleApp.EmailService;
using Sample.Hybrid.ConsoleApp.EmailService.Contract;

using SecretStore;

using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Hybrid;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.Json;

class Program
{
    public static Task Main(string[] args) =>
        Host.CreateDefaultBuilder(args)
         .ConfigureServices((ctx, services) =>
         {
             services.AddHostedService<MainApplication>();

             Secrets.Load(@"..\..\..\..\..\secrets.txt");

             services
                 .AddSlimMessageBus((mbb) =>
                 {
                     // In summary:
                     // - The CustomerChangedEvent messages will be going through the SMB Memory provider.
                     // - The SendEmailCommand messages will be going through the SMB Azure Service Bus provider.
                     // - Each of the bus providers will serialize messages using JSON and use the same DI to resolve consumers/handlers.
                     mbb
                         // Bus 1
                         .AddChildBus("Memory", (mbbChild) =>
                         {
                             mbbChild
                                 .WithProviderMemory()
                                 .AutoDeclareFrom(typeof(CustomerChangedEventHandler).Assembly, consumerTypeFilter: consumerType => consumerType.Namespace.Contains("Application"));
                         })
                         // Bus 2
                         .AddChildBus("AzureSB", (mbbChild) =>
                         {
                             var serviceBusConnectionString = Secrets.Service.PopulateSecrets(ctx.Configuration["Azure:ServiceBus"]);
                             mbbChild
                                 .WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString))
                                 .Produce<SendEmailCommand>(x => x.DefaultQueue("test-ping-queue"))
                                 .Consume<SendEmailCommand>(x => x.Queue("test-ping-queue").WithConsumer<SmtpEmailService>());
                         })
                         .WithProviderHybrid()
                         .AddServicesFromAssemblyContaining<CustomerChangedEventHandler>()
                         .AddJsonSerializer(); // serialization setup will be shared between bus 1 and 2
                 });
         })
         .Build()
         .RunAsync();
}
