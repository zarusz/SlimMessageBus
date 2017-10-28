using System;
using Microsoft.Practices.ServiceLocation;
using Moq;
using SlimMessageBus.Core.Config;
using SlimMessageBus.Sample.DomainEvents.Simple.Events;
using SlimMessageBus.Sample.DomainEvents.Simple.Handlers;
using SlimMessageBus.ServiceLocator.Config;

namespace SlimMessageBus.Sample.DomainEvents.Simple
{
    class Program
    {
        static void Main(string[] args)
        {
            WithJustCore();
            WithServiceLocator();
            Console.ReadLine();
        }

        private static void WithServiceLocator()
        {
            // Mock: the ServiceLocator (start)
            var serviceLocatorMock = new Mock<IServiceLocator>();
            var serviceLocatorProviderMock = new Mock<ServiceLocatorProvider>();
            serviceLocatorProviderMock.Setup(x => x()).Returns(serviceLocatorMock.Object);

            Microsoft.Practices.ServiceLocation.ServiceLocator.SetLocatorProvider(serviceLocatorProviderMock.Object);
            
            // Mock: the ServiceLocator (end)

            // Define the recipie how to create our IMessageBus
            var busBuilder = new MessageBusBuilder()
                .ResolveHandlersFromServiceLocator() // Needs SlimMessageBus.ServiceLocator NuGet package
                .SimpleMessageBus();

            // Mock: Every call to ServiceLocator.GetInstance<IMessageBus>() resolves a new instance
            // The IMessageBus would be bound to the proper scope in your app (per Web Request, per Thread, per Unit Of Work, etc)
            serviceLocatorMock.Setup(x => x.GetInstance<IMessageBus>()).Returns(() =>
            {
                // Create the IMessageBus instance from the builder
                var busInstance = busBuilder
                    .Build();
                return busInstance;
            });

            // Set the provider to resolve our bus - this will lookup the IMessageBus instance in the ServiceLocator.
            MessageBus.SetProvider(() => Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<IMessageBus>());

            // Mock: The handlers are registered in the DI container
            var greeter = new NewUserHelloGreeter();
            serviceLocatorMock.Setup(x => x.GetAllInstances<IHandles<NewUserJoinedEvent>>()).Returns(new[] { greeter });

            // ... Initialization finished


            // Handlers will be executed synchronous.
            // Injected from your DI container or resolved from ServiceLocator
            IMessageBus bus = Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<IMessageBus>(); 
            bus.Publish(new NewUserJoinedEvent("Bob"));
            bus.Publish(new NewUserJoinedEvent("Jane"));

            // OR

            MessageBus.Current.Publish(new NewUserJoinedEvent("Jennifer"));
            MessageBus.Current.Publish(new NewUserJoinedEvent("Tom"));
        }

        public static void WithJustCore()
        {
            // ... Initialization of SlimMessageBus

            // Define the recipie how to create our IMessageBus
            var busBuilder = new MessageBusBuilder()
                .SimpleMessageBus();

            // Create the IMessageBus instance from the builder
            var bus = busBuilder
                .Build();

            // Set the provider to resolve our bus - this setup will work as a singleton.
            MessageBus.SetProvider(() => bus);

            // ... Somewhere in your domain layer

            var greeter = new NewUserHelloGreeter();
            
            // Register handler explicitly
            bus.Subscribe(greeter);

            // Handlers will be executed synchronous.
            bus.Publish(new NewUserJoinedEvent("Bob"));
            bus.Publish(new NewUserJoinedEvent("Jane"));

            // .. OR

            MessageBus.Current.Publish(new NewUserJoinedEvent("Jennifer"));
            MessageBus.Current.Publish(new NewUserJoinedEvent("Tom"));

            bus.UnSubscribe(greeter);
        }
    }
}
