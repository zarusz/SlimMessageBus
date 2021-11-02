namespace SlimMessageBus.Host.Test
{
    using FluentAssertions;
    using SlimMessageBus.Host.Config;
    using Xunit;

    public class ConsumerSettingsTest
    {
        [Fact]
        public void When_SetMessageTypeSet_Given_MessageIsNotRequest_Then_ResponseTypeShouldBeNull()
        {
            // arrange

            // act
            var cs = new ConsumerSettings { MessageType = typeof(SomeMessage) };

            // assert
            cs.ResponseType.Should().BeNull();
            cs.IsRequestMessage.Should().BeFalse();
        }

        [Fact]
        public void When_RequestMessage_GivenRequestMessageWhenSetMessageType_Then_ResponseTypeShouldBeInferred()
        {
            // arrange

            // act
            var cs = new ConsumerSettings { MessageType = typeof(SomeRequest) };

            // assert
            cs.ResponseType.Should().Be(typeof(SomeResponse));
            cs.IsRequestMessage.Should().BeTrue();
        }

        [Fact]
        public void When_Creation_Then_DefaultInstancesIs1()
        {
            // arrange

            // act
            var cs = new ConsumerSettings();

            // assert
            cs.Instances.Should().Be(1);
        }
    }
}