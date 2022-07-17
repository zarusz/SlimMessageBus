namespace SlimMessageBus.Host.Test.Config
{
    using FluentAssertions;
    using SlimMessageBus.Host.Config;
    using Xunit;

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
            copy.Configurators.Should().BeSameAs(prototype.Configurators);
            copy.ChildBuilders.Should().BeSameAs(prototype.ChildBuilders);
            copy.BusName.Should().BeSameAs(prototype.BusName);
            copy.BusFactory.Should().BeSameAs(prototype.BusFactory);
        }
    }
}