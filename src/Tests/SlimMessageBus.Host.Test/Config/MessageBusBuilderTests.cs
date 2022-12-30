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
    public void Given_OtherBuilder_When_CopyConstructorUsed_Then_AllStateIsCopied()
    {
        // arrange
        var prototype = MessageBusBuilder.Create();

        // act
        var copy = new DerivedMessageBusBuilder(prototype);

        // assert
        copy.Settings.Should().BeSameAs(prototype.Settings);
        copy.Settings.Name.Should().BeSameAs(prototype.Settings.Name);
        copy.Configurators.Should().BeSameAs(prototype.Configurators);
        copy.ChildBuilders.Should().BeSameAs(prototype.ChildBuilders);
        copy.BusFactory.Should().BeSameAs(prototype.BusFactory);
    }
}