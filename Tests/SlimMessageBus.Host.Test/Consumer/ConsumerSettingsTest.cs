using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Test.Consumer
{
    [TestClass]
    public class ConsumerSettingsTest
    {
        [TestMethod]
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

        [TestMethod]
        public void AfterSettingMessageType_WhenRequestMessage_ResponseTypeShouldBeInfered()
        {
            // arrange
            var cs = new ConsumerSettings();

            // act
            cs.MessageType = typeof(SomeRequestMessage);

            // assert
            cs.ResponseType.ShouldBeEquivalentTo(typeof(SomeResponseMessage));
            cs.IsRequestMessage.Should().BeTrue();
        }

        [TestMethod]
        public void AfterCreation_DefaultInstances_ShouldBe1()
        {
            // arrange

            // act
            var cs = new ConsumerSettings();

            // assert
            cs.Instances.ShouldBeEquivalentTo(1);
        }
    }
}
