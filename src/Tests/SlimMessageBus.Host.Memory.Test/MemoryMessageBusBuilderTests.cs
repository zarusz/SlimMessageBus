namespace SlimMessageBus.Host.Memory.Test;

using System.Reflection;

using SlimMessageBus.Host;

using static SlimMessageBus.Host.Memory.Test.MemoryMessageBusIt;

public class MemoryMessageBusBuilderTests
{
    private readonly MemoryMessageBusBuilder subject;

    public MemoryMessageBusBuilderTests()
    {
        subject = MessageBusBuilder.Create().WithProviderMemory();
    }

    [Fact]
    public void Given_AssemblyWithConsumers_When_AutoDiscoverConsumers_Then_AllOfThemDeclared()
    {
        // arrange

        // act
        var whitelistNames = new[] { "Ping", "Echo", "Some", "Generic" };
        subject.AutoDeclareFrom(Assembly.GetExecutingAssembly(),
            consumerTypeFilter: (consumerType) => whitelistNames.Any(name => consumerType.Name.Contains(name))); // include specific consumers only

        // assert

        var producers = subject.Settings.Producers;
        var consumers = subject.Settings.Consumers;

        producers.Count.Should().Be(6);

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageA)
            && x.DefaultPath == typeof(SomeMessageA).FullName);

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageB)
            && x.DefaultPath == typeof(SomeMessageB).FullName);

        producers.Should().Contain(x =>
            x.MessageType == typeof(PingMessage)
            && x.DefaultPath == typeof(PingMessage).FullName);

        producers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.DefaultPath == typeof(EchoRequest).FullName);

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequest)
            && x.DefaultPath == typeof(SomeRequest).FullName);

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequestWithoutResponse)
            && x.DefaultPath == typeof(SomeRequestWithoutResponse).FullName);

        consumers.Count.Should().Be(7);

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageA)
            && x.Path == typeof(SomeMessageA).FullName
            && x.Invokers.Count == 2
            && x.Invokers.Any(i => i.ConsumerType == typeof(SomeMessageAConsumer))
            && x.Invokers.Any(i => i.ConsumerType == typeof(SomeMessageAConsumer2)));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageB)
            && x.Path == typeof(SomeMessageB).FullName
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(SomeMessageBConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(PingMessage)
            && x.Path == typeof(PingMessage).FullName
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(PingConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.Path == typeof(EchoRequest).FullName
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(EchoRequestHandler)
            && x.ResponseType == typeof(EchoResponse));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequest)
            && x.Path == typeof(SomeRequest).FullName
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(SomeRequestConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequest)
            && x.Path == typeof(SomeRequest).FullName
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(SomeRequestHandler)
            && x.ResponseType == typeof(SomeResponse));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequestWithoutResponse)
            && x.Path == typeof(SomeRequestWithoutResponse).FullName
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(SomeRequestWithoutResponseHandler)
            && x.ResponseType == null);
    }

    [Fact]
    public void Given_AssemblyWithConsumersAndConsumerFilterSet_When_AutoDiscoverConsumers_Then_OnlySelectedOfThemAreDeclared()
    {
        // arrange

        // act
        subject.AutoDeclareFrom(Assembly.GetExecutingAssembly(),
            consumerTypeFilter: (consumerType) => consumerType.Name.Contains("Ping") || consumerType.Name.Contains("Echo")); // include specific consumers only

        // assert

        var producers = subject.Settings.Producers;
        var consumers = subject.Settings.Consumers;

        producers.Count.Should().Be(2);

        producers.Should().Contain(x =>
            x.MessageType == typeof(PingMessage)
            && x.DefaultPath == typeof(PingMessage).FullName);

        producers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.DefaultPath == typeof(EchoRequest).FullName);

        consumers.Count.Should().Be(2);

        consumers.Should().Contain(x =>
            x.MessageType == typeof(PingMessage)
            && x.Path == typeof(PingMessage).FullName
            && x.ConsumerType == typeof(PingConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.Path == typeof(EchoRequest).FullName
            && x.ResponseType == typeof(EchoResponse)
            && x.ConsumerType == typeof(EchoRequestHandler));
    }

    [Fact]
    public void Given_AssemblyWithConsumers_And_PolymorphicMessages_When_AutoDiscoverConsumers_Then_AllOfThemDeclared_And_TopicGroupedUnderTheRootMessage()
    {
        // arrange

        // act
        subject.AutoDeclareFrom(Assembly.GetExecutingAssembly(),
            consumerTypeFilter: (consumerType) => consumerType.Name.Contains("Customer") || consumerType.Name.Contains("Order")); // include specific consumers only

        // assert

        var producers = subject.Settings.Producers;
        var consumers = subject.Settings.Consumers;

        producers.Count.Should().Be(2);

        producers.Should().Contain(x =>
            x.MessageType == typeof(CustomerEvent)
            && x.DefaultPath == typeof(CustomerEvent).FullName);

        producers.Should().Contain(x =>
            x.MessageType == typeof(OrderShipped)
            && x.DefaultPath == typeof(OrderShipped).FullName);

        consumers.Count.Should().Be(2);

        consumers.Should().Contain(x =>
            x.MessageType == typeof(CustomerEvent)
            && x.Path == typeof(CustomerEvent).FullName
            && x.ConsumerType == typeof(CustomerEventConsumer)
            && x.Invokers.Count == 3
            && x.Invokers.Any(i => i.ConsumerType == typeof(CustomerEventConsumer))
            && x.Invokers.Any(i => i.ConsumerType == typeof(CustomerCreatedCustomer))
            && x.Invokers.Any(i => i.ConsumerType == typeof(CustomerDeletedCustomer)));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(OrderShipped)
            && x.ConsumerType == typeof(OrderShippedConsumer)
            && x.Path == typeof(OrderShipped).FullName
            && x.Invokers.Count == 1
            && x.Invokers.Any(i => i.ConsumerType == typeof(OrderShippedConsumer)));
    }
}

public abstract record CustomerEvent;
public record CustomerCreatedOrUpdated : CustomerEvent;
public record CustomerCreated : CustomerCreatedOrUpdated;
public record CustomerUpdated : CustomerCreatedOrUpdated;
public record CustomerDeleted : CustomerEvent;

public abstract record OrderEvent;
public record OrderShipped : OrderEvent;

public class CustomerEventConsumer : IConsumer<CustomerEvent>
{
    public Task OnHandle(CustomerEvent message, CancellationToken cancellationToken) => throw new NotImplementedException();
}

public class CustomerCreatedCustomer : IConsumer<CustomerCreated>
{
    public Task OnHandle(CustomerCreated message, CancellationToken cancellationToken) => throw new NotImplementedException();
}

public class CustomerDeletedCustomer : IConsumer<CustomerDeleted>
{
    public Task OnHandle(CustomerDeleted message, CancellationToken cancellationToken) => throw new NotImplementedException();
}

public class OrderShippedConsumer : IConsumer<OrderShipped>
{
    public Task OnHandle(OrderShipped message, CancellationToken cancellationToken) => throw new NotImplementedException();
}