namespace Sample.DomainEvents.Domain;

/// <summary>
/// OrderLine entity
/// </summary>
public class OrderLine
{
    public string ProductId { get; private set; }
    public int Quantity { get; protected internal set; }

    protected internal OrderLine(string productId)
    {
        ProductId = productId;
    }
}