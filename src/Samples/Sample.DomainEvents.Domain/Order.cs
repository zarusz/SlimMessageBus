namespace Sample.DomainEvents.Domain;

using SlimMessageBus;

/// <summary>
/// Order aggregate root
/// </summary>
public class Order
{
    public Guid Id { get; }
    public Customer Customer { get; }
    public OrderState State { get; private set; }

    private readonly IList<OrderLine> _lines = [];
    public IEnumerable<OrderLine> Lines => _lines.AsEnumerable();

    public Order(Customer customer)
    {
        Id = Guid.NewGuid();
        Customer = customer;
        State = OrderState.New;
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