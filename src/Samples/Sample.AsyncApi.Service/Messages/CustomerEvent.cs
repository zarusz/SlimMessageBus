namespace Sample.AsyncApi.Service.Messages;

/// <summary>
/// Customer related events that notify about interesting events around customer lifecycle.
/// </summary>
/// <param name="Id"></param>
public record CustomerEvent(Guid Id)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
