using FluentAssertions;
using SlimMessageBus.Host.Config;
using Xunit;

namespace SlimMessageBus.Host.Test
{
    public class ConsumerSettingsTest
    {
        [Fact]
        public void GivenMessageIsNotRequestWhenSetMessageTypeSetThenResponseTypeShouldBeNull()
        {
            // arrange

            // act
            var cs = new ConsumerSettings { MessageType = typeof(SomeMessage) };

            // assert
            cs.ResponseType.Should().BeNull();
            cs.IsRequestMessage.Should().BeFalse();
        }

        [Fact]
        public void GivenRequestMessageWhenSetMessageTypeWhenRequestMessageThenResponseTypeShouldBeInferred()
        {
            // arrange

            // act
            var cs = new ConsumerSettings { MessageType = typeof(SomeRequest) };

            // assert
            cs.ResponseType.Should().Be(typeof(SomeResponse));
            cs.IsRequestMessage.Should().BeTrue();
        }

        [Fact]
        public void WhenCreationThenDefaultInstancesIs1()
        {
            // arrange

            // act
            var cs = new ConsumerSettings();

            // assert
            cs.Instances.Should().Be(1);
        }
    }
}