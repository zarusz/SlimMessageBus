using Sample.Hybrid.ConsoleApp.DomainModel;
using Sample.Hybrid.ConsoleApp.EmailService.Contract;
using SlimMessageBus;
using System.Threading.Tasks;

namespace Sample.Hybrid.ConsoleApp.ApplicationLayer
{
    public class CustomerChangedEventHandler : IConsumer<CustomerEmailChangedEvent>
    {
        private readonly IMessageBus _bus;

        public CustomerChangedEventHandler(IMessageBus bus)
        {
            _bus = bus;
        }

        public async Task OnHandle(CustomerEmailChangedEvent message, string name)
        {
            // Send confirmation email

            var cmd = new SendEmailCommand
            {
                Recipient = message.Customer.Email,
                Title = "Please confirm your email",
                Body = "Click <a href=\"https://google.com\">here</a> to confirm your new email."
            };
            await _bus.Publish(cmd);

            // Do other logic ...
        }
    }
}
