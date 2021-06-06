namespace Sample.Hybrid.ConsoleApp
{
    using System;
    using System.Threading.Tasks;
    using Sample.Hybrid.ConsoleApp.DomainModel;
    using SlimMessageBus;

    public class Application
    {
        private readonly IMessageBus _messageBus;
        private bool _canRun = true;

        // Inject to ensure that bus is started (the singletons are lazy created by default by MS dependency injector)
        public Application(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        public void Run()
        {
            var task = Task.Run(CustomerDoesSomeStuff);

            Console.WriteLine("Application started. Press any key to terminate.");
            Console.ReadLine();

            _canRun = false;
        }

        public async Task CustomerDoesSomeStuff()
        {
            while (_canRun)
            {
                var customer = new Customer("John", "Doe");
                customer.ChangeEmail("john@doe.com");

                await Task.Delay(2000);
            }
        }
    }
}