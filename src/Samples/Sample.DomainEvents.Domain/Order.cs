namespace Sample.DomainEvents.Domain;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlimMessageBus;

/// <summary>
/// Order aggregate root
/// </summary>
public class Order
{
    public Customer Customer { get; }
    public OrderState State { get; private set; }

    private readonly IList<OrderLine> _lines = new List<OrderLine>();
    public IEnumerable<OrderLine> Lines => _lines.AsEnumerable();

    public Order(Customer customer)
    {
        State = OrderState.New;
        Customer = customer;
    }

    public OrderLine Add(string productId, int quantity)
    {
        var orderLine = _lines.SingleOrDefault(x => x.ProductId == productId);
        if (orderLine == null)
        {
            orderLine = new OrderLine(productId);
            _lines.Add(orderLine);
        }
        orderLine.Quantity += quantity;

        return orderLine;
    }

    public async Task Submit()
    {
        State = OrderState.Submitted;

        var e = new OrderSubmittedEvent(this);
        await MessageBus.Current.Publish(e);
    }
}