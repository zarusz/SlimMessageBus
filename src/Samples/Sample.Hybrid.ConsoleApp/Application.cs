namespace Sample.Hybrid.ConsoleApp;

using Microsoft.Extensions.Hosting;

using Sample.Hybrid.ConsoleApp.Domain;

public class MainApplication : IHostedService
{
    private bool canRun = true;

    public async Task CustomerDoesSomeStuff()
    {
        while (canRun)
        {
            var customer = new Customer("John", "Doe");
            await customer.ChangeEmail("john@doe.com");

            await Task.Delay(2000);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var task = Task.Run(CustomerDoesSomeStuff);

        Console.WriteLine("Application started. Press any key to terminate.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        canRun = false;

        return Task.CompletedTask;
    }
}