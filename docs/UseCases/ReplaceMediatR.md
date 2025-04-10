### Use Case: Migrating from MediatR to SlimMessageBus

The SlimMessageBus [in-memory provider](/docs/provider_memory.md) can replace the need to use the [MediatR](https://github.com/jbogard/MediatR) library:

- It has similar semantics and has the [interceptor pipeline](/docs/intro.md#interceptors) enabling the addition of custom behavior.
- The [generic interceptors](/docs/intro.md#generic-interceptors) can introduce common behavior like logging, authorization, or audit of messages.
- The [FluentValidation plugin](/docs/plugin_fluent_validation.md) can introduce request/command/query validation.
- The external communication can be layered on top of SlimMessageBus, enabling one library for in-memory and out-of-process messaging ([Hybrid Provider](/docs/provider_hybrid.md)).

See the [CQRS and FluentValidation](/src/Samples/Sample.ValidatingWebApi/) samples.

---

### Example: Migrating from MediatR to SlimMessageBus

Below are examples demonstrating the migration steps.

### Using MediatR in `Program.cs`

Here is a typical setup for MediatR:

```csharp
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Register MediatR services
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});

var app = builder.Build();

app.MapGet("/ping", async (IMediator mediator) =>
{
    return await mediator.Send(new PingRequest());
});

app.Run();

// Define a simple request and handler
public record PingRequest : IRequest<string>;

public class PingRequestHandler : IRequestHandler<PingRequest, string>
{
    public Task<string> Handle(PingRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult("Pong");
    }
}
```

---

### Migrating to SlimMessageBus in `Program.cs`

#### Install required NuGet packages:

```
dotnet add package SlimMessageBus.Host.Memory
```

#### Example code setup:

```csharp
using SlimMessageBus;
using SlimMessageBus.Host.Memory;

var builder = WebApplication.CreateBuilder(args);

// Configure SlimMessageBus with In-Memory provider
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(Program).Assembly)
        .AddServicesFromAssemblyContaining<Program>();
});

var app = builder.Build();

app.MapGet("/ping", async (IMessageBus bus) =>
{
    return await bus.Send(new PingRequest());
});

app.Run();

// Define request and handler compatible with SlimMessageBus
public record PingRequest : IRequestMessage<string>;

public class PingRequestHandler : IRequestHandler<PingRequest, string>
{
    public async Task<string> OnHandle(PingRequest request, CancellationToken cancellationToken)
    {
        return "Pong";
    }
}
```

### Key Changes:

- Replace `IMediator` with `IMessageBus`.
- Change request interface from `IRequest` to `IRequestMessage`.
- Register SlimMessageBus using `AddSlimMessageBus` and configure with `WithProviderMemory`.
- Handlers now implement `IRequestHandler<TRequest, TResponse>` with method `OnHandle()` instead of MediatR's `Handle()`.

---

### Additional Capabilities in SlimMessageBus:

- **Interceptors**: You can easily add cross-cutting concerns such as logging, authorization, or auditing.
- **FluentValidation Integration**: Direct support for request validation via FluentValidation.
- **Hybrid Messaging**: Seamlessly transition from in-memory to out-of-process messaging if your application evolves (or combine both).

For more advanced usage and additional features, please review the [SlimMessageBus documentation](/docs/).
