### Use Case: Domain Events (in-process pub/sub messaging)

This example shows how the `SlimMessageBus.Host.Memory` transport can be used to implement the Domain Events pattern.
The provider passes messages in the same process (no external message broker is required).

The domain event is a simple POCO:

```cs
// domain event
public record OrderSubmittedEvent(Order Order, DateTime Timestamp);
```

The domain event handler implements the `IConsumer<T>` interface:

```cs
// domain event handler
public class OrderSubmittedHandler : IConsumer<OrderSubmittedEvent>
{
   public Task OnHandle(OrderSubmittedEvent e, CancellationToken cancellationToken)
   {
      // ...
   }
}
```

The domain event handler (consumer) is obtained from the MSDI at the time of event publication.
The event publish enlists in the ongoing scope (web request scope, external message scope of the ongoing message).

In the domain model layer, the domain event gets raised:

```cs
// aggregate root
public class Order
{
   public Customer Customer { get; }
   public OrderState State { get; private set; }

   private IList<OrderLine> lines = new List<OrderLine>();
   public IEnumerable<OrderLine> Lines => lines.AsEnumerable();

   public Order(Customer customer)
   {
      Customer = customer;
      State = OrderState.New;
   }

   public OrderLine Add(string productId, int quantity) { }

   public Task Submit()
   {
      State = OrderState.Submitted;

      // Raise domain event
      return MessageBus.Current.Publish(new OrderSubmittedEvent(this));
   }
}
```

> By default Memory transport has the [Publish operation blocking](/docs/provider_memory.md#asynchronous-publish).

Sample logic executed by the client of the domain model:

```cs
var john = new Customer("John", "Wick");

var order = new Order(john);
order.Add("id_machine_gun", 2);
order.Add("id_grenade", 4);

await order.Submit(); // events fired here
```

Notice the static [`MessageBus.Current`](/src/SlimMessageBus/MessageBus.cs) property is configured to resolve a scoped `IMessageBus` instance (web request-scoped or pick-up message scope from a currently processed message).

The `SlimMessageBus` configuration for the in-memory provider looks like this:

```cs
//IServiceCollection services;

// Configure the message bus
services.AddSlimMessageBus(mbb =>
{
   mbb.WithProviderMemory();
   // Find types that implement IConsumer<T> and IRequestHandler<T, R> and declare producers and consumers on the mbb
   mbb.AutoDeclareFrom(Assembly.GetExecutingAssembly());
   // Scan assembly for consumers, handlers, interceptors, and register into MSDI
   mbb.AddServicesFromAssemblyContaining<OrderSubmittedHandler>();
});
```

For the ASP.NET project, set up the `MessageBus.Current` helper (if you want to use it, and pick up the current web-request scope):

```cs
services.AddSlimMessageBus(mbb =>
{
   // ...
   mbb.AddAspNet(); // requires SlimMessageBus.Host.AspNetCore package
});
services.AddHttpContextAccessor(); // This is required by the SlimMessageBus.Host.AspNetCore plugin
```

See the complete [sample](/src/Samples#sampledomainevents) for ASP.NET Core where the handler and bus are web-request scoped.
