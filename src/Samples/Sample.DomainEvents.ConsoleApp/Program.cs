using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlimMessageBus.Host.Memory;

namespace Sample.DomainEvents.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var mbb = MessageBusBuilder.Create()
                .Publish<OrderSubmittedEvent>(x => x.DefaultTopic(x.MessageType.Name))
                .SubscribeTo<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).Group("").WithSubscriber<OrderSubmittedHandler>())
                //.Do(builder => Assembly.GetExecutingAssembly()
                //        .GetTypes()
                //        .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IConsumer<>))
                //        .ToList().ForEach(consumerType =>
                //        {
                //            var messageType = consumerType.GetGenericArguments()[0];
                //        })
                //)
                .WithDependencyResolver(new LookupDependencyResolver(type =>
                 {
                     // Simulate a dependency container (Autofac, Unity)
                     if (type == typeof(OrderSubmittedHandler)) return new OrderSubmittedHandler();
                     throw new InvalidOperationException();
                 }))
                .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
                .WithProviderMemory(new MemoryMessageBusSettings
                {
                    EnableMessageSerialization = false
                });

            var mb = mbb.Build();
            MessageBus.SetProvider(() => mb);
            //HttpContextAccessor

            var john = new Customer("John", "Whick");

            var order = new Order(john);
            order.Add("id_machine_gun", 2);
            order.Add("id_grenade", 4);

            order.Submit();
        }
    }

    // domain event
    public class OrderSubmittedEvent
    {
        public Order Order { get; }
        public DateTime Timestamp { get; }

        public OrderSubmittedEvent(Order order)
        {
            Order = order;
            Timestamp = DateTime.UtcNow;
        }
    }

    // aggregate root
    public class Order
    {
        public Customer Customer { get; }
        private IList<OrderLine> _lines = new List<OrderLine>();
        public OrderState State { get; private set; }

        public IEnumerable<OrderLine> Lines => _lines.AsEnumerable();

        public Order(Customer customer)
        {
            State = OrderState.New;
            Customer = customer;
        }

        public OrderLine Add(string productId, int quantity)
        {
            var orderLine = _lines.SingleOrDefault(x => x.ProductId == productId);
            if (orderLine == null)
            {
                orderLine = new OrderLine(productId);
                _lines.Add(orderLine);
            }
            orderLine.Quantity += quantity;

            return orderLine;
        }

        public void Submit()
        {
            State = OrderState.Submitted;

            var e = new OrderSubmittedEvent(this);
            MessageBus.Current.Publish(e).Wait();
        }
    }

    public enum OrderState
    {
        New,
        Submitted,
        Fulfilled,
    }

    // entity
    public class OrderLine
    {
        public string ProductId { get; private set; }
        public int Quantity { get; protected internal set; }

        protected internal OrderLine(string productId)
        {
            ProductId = productId;
        }
    }

    // aggregate root
    public class Customer
    {
        public string CustomerId { get; private set; }
        public string Firstname { get; private set; }
        public string Lastname { get; private set; }

        public Customer(string firstname, string lastname)
        {
            CustomerId = Guid.NewGuid().ToString();
            Firstname = firstname;
            Lastname = lastname;
        }
    }

    public class OrderSubmittedHandler : IConsumer<OrderSubmittedEvent>
    {
        public Task OnHandle(OrderSubmittedEvent e, string topic)
        {
            Console.WriteLine("Customer {0} {1} just placed an order for:", e.Order.Customer.Firstname, e.Order.Customer.Lastname);
            foreach (var orderLine in e.Order.Lines)
            {
                Console.WriteLine("- {0}x {1}", orderLine.Quantity, orderLine.ProductId);
            }

            Console.WriteLine("Generating a shipping order...");
            return Task.Delay(1000);
        }
    }
}

