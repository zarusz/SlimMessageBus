namespace Sample.DomainEvents.Application.DomainEventHandlers;

using Microsoft.Extensions.Logging;

using Sample.DomainEvents.Domain;

using SlimMessageBus;

/// <summary>
/// The domain event handler for <see cref="OrderSubmittedEvent"/>
/// </summary>
public class OrderSubmittedHandler(ILogger<OrderSubmittedHandler> logger) : IConsumer<OrderSubmittedEvent>
{
    public Task OnHandle(OrderSubmittedEvent e, CancellationToken cancellationToken)
    {
        logger.LogInformation("Customer {Firstname} {Lastname} just placed an order for:", e.Order.Customer.Firstname, e.Order.Customer.Lastname);
        foreach (var orderLine in e.Order.Lines)
        {
            logger.LogInformation("- {Quantity}x {ProductId}", orderLine.Quantity, orderLine.ProductId);
        }

        logger.LogInformation("Generating a shipping order...");
        return Task.Delay(1000, cancellationToken);
    }
}
