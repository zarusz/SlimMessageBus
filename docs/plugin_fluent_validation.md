# FluentValidation Plugin for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
  - [Consumer side validation](#consumer-side-validation)
  
## Introduction

The `SlimMessageBus.Host.FluentValidation` introduces validation on the producer or consumer side by leveraging the [FluentValidation](https://www.nuget.org/packages/FluentValidation) library.
The plugin is based on `SlimMessageBus.Host.Interceptor` core interfaces and can work with any transport including the memory bus.

## Configuration

Consider the following command, with the validator (using FluentValidation) and command handler:

```cs
// The command
public record CreateCustomerCommand : IRequestMessage<CommandResultWithId>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

// The validator of the command (using FluentValidation)
public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.LastName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty().Length(6).When(x => x.Phone != null);
    }
}

// The handler of the command
public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CommandResultWithId>
{
    public async Task<CommandResultWithId> OnHandle(CreateCustomerCommand command, string path)
    {
        // ...
    }
}
```

Consider an in-process command that is delivered using the memory bus:

```cs
// Using minimal APIs
var builder = WebApplication.CreateBuilder(args);

// Configure SMB
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .WithProviderMemory()
        .AutoDeclareFrom(Assembly.GetExecutingAssembly());
}, addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() });

// Register FluentValidation validators
// Package: FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();
// Register validation interceptors for message (here command) producers inside message bus
// Package: SlimMessageBus.Host.FluentValidation.MsDependencyInjection
builder.Services.AddProducerValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();
```

> The `AddProducerValidatorsFromAssemblyContaining` will register an SMB interceptor that will validate the message upon `.Publish()` or `.Send()` - on the producer side before the message even gets deliverd to the underlying transport.

For example given an ASP.NET Mimimal WebApi, the request can be delegated to SlimMessageBus in memory transport:

```cs
// Using minimal APIs
var app = builder.Build();

app.MapPost("/customer", (CreateCustomerCommand command, IMessageBus bus) => bus.Send(command));    

await app.RunAsync();
```

In the situation that the incomming HTTP request where to deliver an invalid command, the request will fail with `FluentValidation.ValidationException: Validation failed` exception.

For full example, please see the [Sample.ValidatingWebApi](../src/Samples/Sample.ValidatingWebApi/) sample.

### Consumer side validation

We can also enable validation of the incoming message just before it gets delivered to the respective `IConsumer<T>` or `IRequestHandler<T, R>` - on the consumer side.
Such validation would be needed in scenarios when an external system delivers messages onto the transport (Kafka, Azure Service Bus) which we do not trust, and therefore we could enable validation on the consumer end. This will prevent the invalid messages to enter the consumer or handler.

```cs
// Using minimal APIs
var builder = WebApplication.CreateBuilder(args);

// Configure SMB
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .WithProviderMemory()
        .AutoDeclareFrom(Assembly.GetExecutingAssembly());
}, addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() });

// Register FluentValidation validators
// Package: FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();
// Register validation interceptors for message (here command) consumers inside message bus
// Package: SlimMessageBus.Host.FluentValidation.MsDependencyInjection
builder.Services.AddConsumerValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();
```

In the situation that the message is invalid, the message will fail with `FluentValidation.ValidationException: Validation failed` exception and standard consumer error handling will take place (depending on the underlying transport it might get retried multiple times until it ends up on dead-letter queue).
