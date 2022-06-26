namespace Sample.Hybrid.ConsoleApp;

using System;
using System.Threading.Tasks;
using Sample.Hybrid.ConsoleApp.Domain;
using SlimMessageBus;

public class MainApplication
{
    private readonly IMessageBus bus;
    private bool canRun = true;

    // Inject to ensure that bus is started (the singletons are lazy created by default by MS dependency injector)
    public MainApplication(IMessageBus bus) => this.bus = bus;

    public void Run()
    {
        var task = Task.Run(CustomerDoesSomeStuff);

        Console.WriteLine("Application started. Press any key to terminate.");
        Console.ReadLine();

        canRun = false;
    }

    public async Task CustomerDoesSomeStuff()
    {
        while (canRun)
        {
            var customer = new Customer("John", "Doe");
            await customer.ChangeEmail("john@doe.com");

            await Task.Delay(2000);
        }
    }
}