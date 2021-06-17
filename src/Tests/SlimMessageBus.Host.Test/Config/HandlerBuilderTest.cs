namespace SlimMessageBus.Host.Test.Config
{
    using FluentAssertions;
    using SlimMessageBus.Host.Config;
    using Xunit;

    public class HandlerBuilderTest
    {       
        [Fact]
        public void BuildsProperSettings()
        {
            // arrange
            var path = "topic";
            var settings = new MessageBusSettings();

            // act
            var subject = new HandlerBuilder<SomeRequest, SomeResponse>(settings)
                .Topic(path)
                .Instances(3)
                .WithHandler<SomeRequestMessageHandler>();

            // assert
            subject.ConsumerSettings.MessageType.Should().Be(typeof(SomeRequest));
            subject.MessageType.Should().Be(typeof(SomeRequest));
            subject.Path.Should().Be(path);
            subject.ConsumerSettings.Path.Should().Be(path);
            subject.ConsumerSettings.Instances.Should().Be(3);
            subject.ConsumerSettings.ConsumerType.Should().Be(typeof(SomeRequestMessageHandler));
            subject.ConsumerSettings.ConsumerMode.Should().Be(ConsumerMode.RequestResponse);
            subject.ConsumerSettings.IsRequestMessage.Should().BeTrue();
            subject.ConsumerSettings.ResponseType.Should().Be(typeof(SomeResponse));
        }
    }
}