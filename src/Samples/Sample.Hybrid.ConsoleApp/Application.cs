using System;
using SlimMessageBus;

namespace Sample.Hybrid.ConsoleApp
{
    public class Application
    {
        private readonly IMessageBus _messageBus;

        public Application(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        public void Run()
        {
            Console.WriteLine("Application started. Press any key to terminate.");
            Console.ReadLine();
        }
    }
}