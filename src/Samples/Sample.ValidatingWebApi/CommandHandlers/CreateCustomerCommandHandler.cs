namespace Sample.ValidatingWebApi.CommandHandlers;

using Sample.ValidatingWebApi.Commands;
using SlimMessageBus;
using System.Threading.Tasks;

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CommandResultWithId>
{
    public async Task<CommandResultWithId> OnHandle(CreateCustomerCommand command, string path)
    {
        return new CommandResultWithId(Guid.NewGuid());
    }
}

