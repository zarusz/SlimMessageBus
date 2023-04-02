namespace Sample.AsyncApi.Service.Messages;

/// <summary>
/// Event when a customer is created within the domain.
/// </summary>
/// <param name="Id"></param>
/// <param name="Firstname"></param>
/// <param name="Lastname"></param>
public record CustomerCreatedEvent(Guid Id, string Firstname, string Lastname) : CustomerEvent(Id);
