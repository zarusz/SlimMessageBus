namespace Sample.OutboxWebApi.Application;

using SlimMessageBus;

public record CreateCustomerCommand(string Firstname, string Lastname) : IRequest<Guid>;
