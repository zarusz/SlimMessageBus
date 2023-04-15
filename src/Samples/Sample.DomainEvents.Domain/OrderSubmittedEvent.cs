namespace Sample.DomainEvents.Domain;

/// <summary>
/// Domain event
/// </summary>
public record OrderSubmittedEvent(Order Order)
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}