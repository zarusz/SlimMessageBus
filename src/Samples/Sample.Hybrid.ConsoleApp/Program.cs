using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sample.Hybrid.ConsoleApp.DomainModel;

namespace Sample.Hybrid.ConsoleApp
{
    class Program
    {
        private static volatile bool CanRun = true;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing...");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            // Create a service collection and configure dependencies
            var serviceCollection = new ServiceCollection();

            var startup = new Startup(configuration);
            startup.ConfigureServices(serviceCollection);

            // Build the our IServiceProvider
            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                // Run the application
                scope.ServiceProvider.GetService<Application>().Run();

                var task = Task.Factory.StartNew(CustomerDoesSomeStuff, TaskCreationOptions.LongRunning);

                // Wait until user hits any key
                Console.ReadKey();

                CanRun = false;
                await task;
            }

        }
        public static void CustomerDoesSomeStuff()
        {
            while (CanRun)
            {
                var customer = new Customer("John", "Doe");
                customer.ChangeEmail("john@doe.com");

                Thread.Sleep(2000);
            }
        }
    }
}
