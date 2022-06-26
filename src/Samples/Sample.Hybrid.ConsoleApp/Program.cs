namespace Sample.Hybrid.ConsoleApp;

using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecretStore;
using SlimMessageBus;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Initializing...");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();

        var loggerFactory = LoggerFactory.Create(cfg => cfg.AddConfiguration(configuration.GetSection("Logging")).AddConsole());

        // Local file with secrets
        Secrets.Load(@"..\..\..\..\..\secrets.txt");

        // Create a service collection and configure dependencies
        var serviceCollection = new ServiceCollection();

        var startup = new Startup(configuration);
        startup.ConfigureServices(serviceCollection);

        // Build the our IServiceProvider
        var serviceProvider = serviceCollection.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();

        MessageBus.SetProvider(() => scope.ServiceProvider.GetRequiredService<IMessageBus>());
        
        // Run the application
        scope.ServiceProvider.GetRequiredService<MainApplication>().Run();
    }
}
