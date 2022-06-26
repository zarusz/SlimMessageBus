namespace Sample.Hybrid.ConsoleApp.Application;

using Sample.Hybrid.ConsoleApp.Domain;
using Sample.Hybrid.ConsoleApp.EmailService.Contract;
using SlimMessageBus;
using System.Threading.Tasks;

public class CustomerChangedEventHandler : IConsumer<CustomerEmailChangedEvent>
{
    private readonly IMessageBus bus;

    public CustomerChangedEventHandler(IMessageBus bus) => this.bus = bus;

    public async Task OnHandle(CustomerEmailChangedEvent message, string name)
    {
        // Send confirmation email

        var cmd = new SendEmailCommand
        {
            Recipient = message.Customer.Email,
            Title = "Please confirm your email",
            Body = "Click <a href=\"https://google.com\">here</a> to confirm your new email."
        };
        await bus.Publish(cmd);

        // Do other logic ...
    }
}
