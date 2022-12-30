namespace Sample.OutboxWebApi.Application;

public record CustomerCreatedEvent(Guid Id, string Firstname, string Lastname);
