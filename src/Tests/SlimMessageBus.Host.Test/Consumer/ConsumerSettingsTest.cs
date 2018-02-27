using FluentAssertions;
using SlimMessageBus.Host.Config;
using Xunit;

namespace SlimMessageBus.Host.Test
{
    public class ConsumerSettingsTest
    {
        [Fact]
        public void AfterSettingMessageType_WhenNoRequestMessage_ResponseTypeShouldBeNull()
        {
            // arrange
            var cs = new ConsumerSettings();

            // act
            cs.MessageType = typeof (SomeMessage);

            // assert
            cs.ResponseType.Should().BeNull();
            cs.IsRequestMessage.Should().BeFalse();
        }

        [Fact]
        public void AfterSettingMessageType_WhenRequestMessage_ResponseTypeShouldBeInfered()
        {
            // arrange
            var cs = new ConsumerSettings();

            // act
            cs.MessageType = typeof(SomeRequest);

            // assert
            cs.ResponseType.Should().Be(typeof(SomeResponse));
            cs.IsRequestMessage.Should().BeTrue();
        }

        [Fact]
        public void AfterCreation_DefaultInstances_ShouldBe1()
        {
            // arrange

            // act
            var cs = new ConsumerSettings();

            // assert
            cs.Instances.Should().Be(1);
        }
    }
}
