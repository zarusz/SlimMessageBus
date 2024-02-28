namespace SlimMessageBus.Host.Test.Config;

using SlimMessageBus.Host.Serialization.Json;

public class MessageBusBuilderTests
{
    internal class DerivedMessageBusBuilder(MessageBusBuilder other) : MessageBusBuilder(other)
    {
    }

    [Fact]
    public void When_WithSerializer_Given_TypeDoesNotImplementIMessageSerializer_Then_ThrowsException()
    {
        // arrange
        var subject = MessageBusBuilder.Create();

        // act
        Action act = () => subject.WithSerializer(typeof(SomeMessage));

        // assert
        act.Should().Throw<ConfigurationMessageBusException>();
    }

    [Fact]
    public void When_WithSerializer_Given_DoesImplementIMessageSerializer_Then_ThrowsException()
    {
        // arrange
        var subject = MessageBusBuilder.Create();

        // act
        Action act = () => subject.WithSerializer<JsonMessageSerializer>();

        // assert
        act.Should().NotThrow<ConfigurationMessageBusException>();
    }

    [Fact]
    public void When_Consume_Given_NoDeclaredConsumerType_Then_DefaultConsumerTypeSet()
    {
        // arrange
        var subject = MessageBusBuilder.Create();

        // act
        subject.Consume<SomeMessage>(x => { });

        // assert
        subject.Settings.Consumers.Count.Should().Be(1);
        subject.Settings.Consumers[0].MessageType.Should().Be(typeof(SomeMessage));
        subject.Settings.Consumers[0].ConsumerType.Should().Be(typeof(IConsumer<SomeMessage>));
    }

    [Fact]
    public void When_Handle_Given_NoDeclaredHandlerType_Then_DefaultHandlerTypeSet()
    {
        // arrange
        var subject = MessageBusBuilder.Create();

        // act
        subject.Handle<SomeRequest, SomeResponse>(x => { });

        // assert
        subject.Settings.Consumers.Count.Should().Be(1);
        subject.Settings.Consumers[0].MessageType.Should().Be(typeof(SomeRequest));
        subject.Settings.Consumers[0].ResponseType.Should().Be(typeof(SomeResponse));
        subject.Settings.Consumers[0].ConsumerType.Should().Be(typeof(IRequestHandler<SomeRequest, SomeResponse>));
    }

    [Fact]
    public void When_Handle_Given_NoDeclaredHandlerType_And_RequestWithoutResponse_Then_DefaultHandlerTypeSet()
    {
        // arrange
        var subject = MessageBusBuilder.Create();

        // act
        subject.Handle<SomeRequestWithoutResponse>(x => { });

        // assert
        subject.Settings.Consumers.Count.Should().Be(1);
        subject.Settings.Consumers[0].MessageType.Should().Be(typeof(SomeRequestWithoutResponse));
        subject.Settings.Consumers[0].ResponseType.Should().BeNull();
        subject.Settings.Consumers[0].ConsumerType.Should().Be(typeof(IRequestHandler<SomeRequestWithoutResponse>));
    }

    [Fact]
    public void Given_OtherBuilder_When_CopyConstructorUsed_Then_AllStateIsCopied()
    {
        // arrange
        var subject = MessageBusBuilder.Create();
        subject.WithProvider(Mock.Of<Func<MessageBusSettings, IMessageBus>>());
        subject.AddChildBus("Bus1", mbb => { });

        // act
        var copy = new DerivedMessageBusBuilder(subject);

        // assert
        copy.Settings.Should().BeSameAs(subject.Settings);
        copy.Settings.Name.Should().BeSameAs(subject.Settings.Name);
        copy.Children.Should().BeSameAs(subject.Children);
        copy.BusFactory.Should().BeSameAs(subject.BusFactory);
        copy.PostConfigurationActions.Should().BeSameAs(subject.PostConfigurationActions);
    }
}