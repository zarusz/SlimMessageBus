namespace Sample.ValidatingWebApi.Commands;

using SlimMessageBus;

public record CreateCustomerCommand : IRequest<CommandResultWithId>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
