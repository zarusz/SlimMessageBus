namespace Sample.DomainEvents.Application;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sample.DomainEvents.Domain;
using SlimMessageBus;

/// <summary>
/// The domain event handler for <see cref="OrderSubmittedEvent"/>
/// </summary>
public class OrderSubmittedHandler : IConsumer<OrderSubmittedEvent>
{
    private readonly ILogger _logger;

    public OrderSubmittedHandler(ILogger<OrderSubmittedHandler> logger)
    {
        _logger = logger;
    }

    public Task OnHandle(OrderSubmittedEvent e, string name)
    {
        _logger.LogInformation("Customer {0} {1} just placed an order for:", e.Order.Customer.Firstname, e.Order.Customer.Lastname);
        foreach (var orderLine in e.Order.Lines)
        {
            _logger.LogInformation("- {0}x {1}", orderLine.Quantity, orderLine.ProductId);
        }

        _logger.LogInformation("Generating a shipping order...");
        return Task.Delay(1000);
    }
}

