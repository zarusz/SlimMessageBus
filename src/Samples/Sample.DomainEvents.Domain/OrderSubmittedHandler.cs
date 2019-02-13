using System.Globalization;
using System.Threading.Tasks;
using Common.Logging;
using SlimMessageBus;

namespace Sample.DomainEvents.Domain
{
    /// <summary>
    /// The domain event handler for <see cref="OrderSubmittedEvent"/>
    /// </summary>
    public class OrderSubmittedHandler : IConsumer<OrderSubmittedEvent>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OrderSubmittedHandler));

        public Task OnHandle(OrderSubmittedEvent e, string name)
        {
            Log.InfoFormat(CultureInfo.InvariantCulture, "Customer {0} {1} just placed an order for:", e.Order.Customer.Firstname, e.Order.Customer.Lastname);
            foreach (var orderLine in e.Order.Lines)
            {
                Log.InfoFormat(CultureInfo.InvariantCulture, "- {0}x {1}", orderLine.Quantity, orderLine.ProductId);
            }

            Log.InfoFormat(CultureInfo.InvariantCulture, "Generating a shipping order...");
            return Task.Delay(1000);
        }
    }
}

