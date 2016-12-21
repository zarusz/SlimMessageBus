using System;
using Sample.Images.Messages;
using SlimMessageBus.Config;

namespace Sample.Images.Worker
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var container = ContainerSetup.Create())
            {

                Console.WriteLine("Press enter to stop the application...");
                Console.ReadLine();
            }

        }
    }
}
