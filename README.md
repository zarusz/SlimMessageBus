# SlimMessageBus

SlimMessageBus is a helper for .NET that helps achieve design patterns requiring to pass messages in your application:
* Domain Events

### Usage example (basic)

This example is the simplest usage of `SlimMessageBus` and `SlimMessageBus.Core` to implement Domain Events.

... Somewhere in your domain layer a domain event gets published

```cs
// Option 1
IMessageBus bus = ... // Injected from your favorite DI container
bus.Publish(new NewUserJoinedEvent("Jane"));

// OR Option 2
MessageBus.Current.Publish(new NewUserJoinedEvent("Jennifer"));
```

... The domain event is a simple POCO

```cs
public class NewUserJoinedEvent
{
	public string FullName { get; protected set; }

	public NewUserJoinedEvent(string fullName)
	{
		FullName = fullName;
	}
}
```

... The event handler implements the `IHandles<T>` interface

```cs
public class NewUserHelloGreeter : IHandles<NewUserJoinedEvent>
{
    public void Handle(NewUserJoinedEvent message)
    {
        Console.WriteLine("Hello {0}", message.FullName);
    }
}
```

... The handler can be subscribed explicitly

```cs
// Get a hold of the handler
var greeter = new NewUserHelloGreeter();

// Register handler explicitly
bus.Subscribe(greeter);

// Events are being published here 

bus.UnSubscribe(greeter);
```

... Or when you decide to use one of the container integrations  (e.g. `SlimMessageBus.Autofac`) the handlers can be resolved from the DI container. For more details see other examples.

#### Setup

This is how you set the 'SlimMessageBus' up in the simplest use case.

```cs
// Define the recipie how to create our IMessageBus
var busBuilder = new MessageBusBuilder()
    .SimpleMessageBus();

// Create the IMessageBus instance from the builder
IMessageBus bus = busBuilder
    .Build();

// Set the provider to resolve our bus - this setup will work as a singleton.
MessageBus.SetProvider(() => bus);
```

##Key elements of SlimMessageBus
ToDo
* `IMessageBus`
* `IHandles<TMessage>`
* `MessageBus`

##Features
* Slim and common Pub/Sub messaging interface (2 interfaces + 1 class).
* Synchronous handler execution.
* In-process (same app domain) message passing.
* *[Pending]* Integrations with popular DI containers.
* *[Roadmap]* Asynchronous handler execution.
* *[Roadmap]* Out-of-process (another app domain) message passing.
* *[Roadmap]* Simple routing.
* *[Roadmap]* Reply-To semantic.

##Principles
* The core interface `SlimMessageBus` is slim
  * Depens only on `Common.Logging`.
  * Very generic.
* Selectively add features you really need (e.g. async handler execution or autofac integration).
* Fluent configuration.

##Packages
Name | Descripton | Dependencies
------------ | ------------- | -------------
`SlimMessageBus` | The interfaces to work with SlimMessageBus | `Common.Logging`
`SlimMessageBus.Core` | The minimal in-process, synchronous messsage passing implementation | `SlimMessageBus`
`SlimMessageBus.ServiceLocator` | Extension that resolves handlers from ServiceLocator | `SlimMessageBus.Core` `CommonServiceLocator`
`SlimMessageBus.Autofac` (pending) | Extension that resolves handlers from Autofac DI container | `SlimMessageBus.Core` `Autofac`

##License

[Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0)
