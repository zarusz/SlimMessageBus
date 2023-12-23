namespace Sample.OutboxWebApi.Application;

public record CreateCustomerCommand(string Firstname, string Lastname) : IRequest<Guid>;
