namespace SlimMessageBus.Host.Memory.Test;

using FluentAssertions;
using SlimMessageBus.Host.Config;
using System.Reflection;
using Xunit;
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
        subject.AutoDeclareFromConsumers(Assembly.GetExecutingAssembly());

        // assert

        var producers = subject.Settings.Producers;
        var consumers = subject.Settings.Consumers;

        producers.Count.Should().Be(4);
        producers.Should().Contain(x => x.MessageType == typeof(SomeMessageA) && x.DefaultPath == nameof(SomeMessageA));
        producers.Should().Contain(x => x.MessageType == typeof(SomeMessageB) && x.DefaultPath == nameof(SomeMessageB));
        producers.Should().Contain(x => x.MessageType == typeof(PingMessage) && x.DefaultPath == nameof(PingMessage));
        producers.Should().Contain(x => x.MessageType == typeof(EchoRequest) && x.DefaultPath == nameof(EchoRequest));

        consumers.Count.Should().Be(5);
        consumers.Should().Contain(x => x.MessageType == typeof(SomeMessageA) && x.Path == nameof(SomeMessageA) && x.ConsumerType == typeof(SomeMessageAConsumer));
        consumers.Should().Contain(x => x.MessageType == typeof(SomeMessageA) && x.Path == nameof(SomeMessageA) && x.ConsumerType == typeof(SomeMessageAConsumer2));
        consumers.Should().Contain(x => x.MessageType == typeof(SomeMessageB) && x.Path == nameof(SomeMessageB) && x.ConsumerType == typeof(SomeMessageBConsumer));
        consumers.Should().Contain(x => x.MessageType == typeof(PingMessage) && x.Path == nameof(PingMessage) && x.ConsumerType == typeof(PingConsumer));
        consumers.Should().Contain(x => x.MessageType == typeof(EchoRequest) && x.Path == nameof(EchoRequest) && x.ResponseType == typeof(EchoResponse) && x.ConsumerType == typeof(EchoRequestHandler));
    }

    [Fact]
    public void Given_AssemblyWithConsumersAndConsumerFilterSet_When_AutoDiscoverConsumers_Then_OnlySelectedOfThemAreDeclared()
    {
        // arrange

        // act
        subject.AutoDeclareFromConsumers(Assembly.GetExecutingAssembly(), 
            consumerTypeFilter: (consumerType) => !consumerType.Name.Contains("Some")); // exclude consumers with Some in the name

        // assert

        var producers = subject.Settings.Producers;
        var consumers = subject.Settings.Consumers;

        producers.Count.Should().Be(2);
        producers.Should().Contain(x => x.MessageType == typeof(PingMessage) && x.DefaultPath == nameof(PingMessage));
        producers.Should().Contain(x => x.MessageType == typeof(EchoRequest) && x.DefaultPath == nameof(EchoRequest));

        consumers.Count.Should().Be(2);
        consumers.Should().Contain(x => x.MessageType == typeof(PingMessage) && x.Path == nameof(PingMessage) && x.ConsumerType == typeof(PingConsumer));
        consumers.Should().Contain(x => x.MessageType == typeof(EchoRequest) && x.Path == nameof(EchoRequest) && x.ResponseType == typeof(EchoResponse) && x.ConsumerType == typeof(EchoRequestHandler));
    }
}
