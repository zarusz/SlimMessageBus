namespace Sample.ValidatingWebApi.Commands;

// doc:fragment:Example
public record CreateCustomerCommand : IRequest<CreateCustomerCommandResult>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
// doc:fragment:Example
