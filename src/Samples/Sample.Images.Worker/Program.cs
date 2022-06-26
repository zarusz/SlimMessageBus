namespace Sample.Images.Worker;

using System;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SlimMessageBus;

public class Program
{
    public static void Main()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var loggerFactory = LoggerFactory.Create(cfg => cfg.AddConfiguration(configuration.GetSection("Logging")).AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("Starting worker...");
        using (var container = ContainerSetup.Create(configuration, loggerFactory))
        {
            // eager load the singleton, so that is starts consuming messages
            var messageBus = container.Resolve<IMessageBus>();
            logger.LogInformation("Worker ready");

            Console.WriteLine("Press enter to stop the application...");
            Console.ReadLine();

            logger.LogInformation("Stopping worker...");
        }
        logger.LogInformation("Worker stopped");
    }
}
