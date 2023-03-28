namespace SlimMessageBus.Host.Memory.Test;

using SlimMessageBus.Host;

using System.Reflection;

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
        subject.AutoDeclareFrom(Assembly.GetExecutingAssembly(),
            consumerTypeFilter: (consumerType) => consumerType.Name.Contains("Ping") || consumerType.Name.Contains("Echo") || consumerType.Name.Contains("Some")); // include specific consumers only

        // assert

        var producers = subject.Settings.Producers;
        var consumers = subject.Settings.Consumers;

        producers.Count.Should().Be(6);

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageA)
            && x.DefaultPath == nameof(SomeMessageA));

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageB)
            && x.DefaultPath == nameof(SomeMessageB));

        producers.Should().Contain(x =>
            x.MessageType == typeof(PingMessage)
            && x.DefaultPath == nameof(PingMessage));

        producers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.DefaultPath == nameof(EchoRequest));

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequest)
            && x.DefaultPath == nameof(SomeRequest));

        producers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequestWithoutResponse)
            && x.DefaultPath == nameof(SomeRequestWithoutResponse));

        consumers.Count.Should().Be(7);

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageA)
            && x.Path == nameof(SomeMessageA)
            && x.Invokers.Count == 2
            && x.Invokers.Any(i => i.ConsumerType == typeof(SomeMessageAConsumer))
            && x.Invokers.Any(i => i.ConsumerType == typeof(SomeMessageAConsumer2)));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeMessageB)
            && x.Path == nameof(SomeMessageB)
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(SomeMessageBConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(PingMessage)
            && x.Path == nameof(PingMessage)
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(PingConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.Path == nameof(EchoRequest)
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(EchoRequestHandler)
            && x.ResponseType == typeof(EchoResponse));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequest)
            && x.Path == nameof(SomeRequest)
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(SomeRequestConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequest)
            && x.Path == nameof(SomeRequest)
            && x.Invokers.Count == 1
            && x.ConsumerType == typeof(SomeRequestHandler)
            && x.ResponseType == typeof(SomeResponse));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(SomeRequestWithoutResponse)
            && x.Path == nameof(SomeRequestWithoutResponse)
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
            && x.DefaultPath == nameof(PingMessage));

        producers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.DefaultPath == nameof(EchoRequest));

        consumers.Count.Should().Be(2);

        consumers.Should().Contain(x =>
            x.MessageType == typeof(PingMessage)
            && x.Path == nameof(PingMessage)
            && x.ConsumerType == typeof(PingConsumer));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(EchoRequest)
            && x.Path == nameof(EchoRequest)
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
            && x.DefaultPath == nameof(CustomerEvent));

        producers.Should().Contain(x =>
            x.MessageType == typeof(OrderShipped)
            && x.DefaultPath == nameof(OrderShipped));

        consumers.Count.Should().Be(2);

        consumers.Should().Contain(x =>
            x.MessageType == typeof(CustomerEvent)
            && x.Path == nameof(CustomerEvent)
            && x.ConsumerType == typeof(CustomerEventConsumer)
            && x.Invokers.Count == 3
            && x.Invokers.Any(i => i.ConsumerType == typeof(CustomerEventConsumer))
            && x.Invokers.Any(i => i.ConsumerType == typeof(CustomerCreatedCustomer))
            && x.Invokers.Any(i => i.ConsumerType == typeof(CustomerDeletedCustomer)));

        consumers.Should().Contain(x =>
            x.MessageType == typeof(OrderShipped)
            && x.ConsumerType == typeof(OrderShippedConsumer)
            && x.Path == nameof(OrderShipped)
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
    public Task OnHandle(CustomerEvent message) => throw new NotImplementedException();
}

public class CustomerCreatedCustomer : IConsumer<CustomerCreated>
{
    public Task OnHandle(CustomerCreated message) => throw new NotImplementedException();
}

public class CustomerDeletedCustomer : IConsumer<CustomerDeleted>
{
    public Task OnHandle(CustomerDeleted message) => throw new NotImplementedException();
}

public class OrderShippedConsumer : IConsumer<OrderShipped>
{
    public Task OnHandle(OrderShipped message) => throw new NotImplementedException();
}