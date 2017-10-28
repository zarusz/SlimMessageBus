using System;
using Autofac;
using Common.Logging;
using SlimMessageBus;

namespace Sample.Images.Worker
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger<Program>();

        static void Main(string[] args)
        {
            Log.Info("Starting worker...");
            using (var container = ContainerSetup.Create())
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
