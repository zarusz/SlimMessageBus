﻿namespace Sample.DomainEvents.Domain;

/// <summary>
/// Domain event
/// </summary>
public record OrderSubmittedEvent
{
    public Order Order { get; }
    public DateTime Timestamp { get; }

    public OrderSubmittedEvent(Order order)
    {
        Order = order;
        Timestamp = DateTime.UtcNow;
    }
}