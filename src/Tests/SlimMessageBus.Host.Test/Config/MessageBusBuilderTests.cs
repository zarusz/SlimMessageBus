namespace SlimMessageBus.Host.Test.Config;

using SlimMessageBus.Host.Config;

public class MessageBusBuilderTests
{
    internal class DerivedMessageBusBuilder : MessageBusBuilder
    {
        public DerivedMessageBusBuilder(MessageBusBuilder other) : base(other)
        {
        }
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
    public void Given_OtherBuilder_When_CopyConstructorUsed_Then_AllStateIsCopied()
    {
        // arrange
        var subject = MessageBusBuilder.Create();

        // act
        var copy = new DerivedMessageBusBuilder(subject);

        // assert
        copy.Settings.Should().BeSameAs(subject.Settings);
        copy.Settings.Name.Should().BeSameAs(subject.Settings.Name);
        copy.Configurators.Should().BeSameAs(subject.Configurators);
        copy.ChildBuilders.Should().BeSameAs(subject.ChildBuilders);
        copy.BusFactory.Should().BeSameAs(subject.BusFactory);
    }
}