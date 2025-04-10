namespace Sample.ValidatingWebApi.Commands;

// doc:fragment:Example
public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CreateCustomerCommandResult>
{
    public Task<CreateCustomerCommandResult> OnHandle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CreateCustomerCommandResult(Guid.NewGuid()));
    }
}
// doc:fragment:Example
