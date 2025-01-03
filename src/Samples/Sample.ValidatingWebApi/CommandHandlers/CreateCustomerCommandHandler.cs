namespace Sample.ValidatingWebApi.CommandHandlers;

using Sample.ValidatingWebApi.Commands;

using SlimMessageBus;

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CommandResultWithId>
{
    public Task<CommandResultWithId> OnHandle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CommandResultWithId(Guid.NewGuid()));
    }
}

