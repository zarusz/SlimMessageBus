using System;
using System.Reflection;
using Autofac;
using Common.Logging;
using Common.Logging.Configuration;
using Microsoft.Extensions.Configuration;
using SlimMessageBus;

namespace Sample.Images.Worker
{
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void Main()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var logConfiguration = new LogConfiguration();
            configuration.GetSection("LogConfiguration").Bind(logConfiguration);
            LogManager.Configure(logConfiguration);

            Log.Info("Starting worker...");
            using (var container = ContainerSetup.Create(configuration))
            {
                var messagBus = container.Resolve<IMessageBus>();
                Log.Info("Worker ready");

                Console.WriteLine("Press enter to stop the application...");
                Console.ReadLine();

                Log.Info("Stopping worker...");
            }
            Log.Info("Worker stopped");
        }
    }
}
