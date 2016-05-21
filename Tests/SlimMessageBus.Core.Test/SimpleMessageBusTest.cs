using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace SlimMessageBus.Core.Test
{
    /// <summary>
    /// Unit test of <see cref="SimpleMessageBus"/>
    /// </summary>
    [TestClass]
    public class SimpleMessageBusTest
    {
        private SimpleMessageBus _bus;

        [TestInitialize]
        public void InitTest()
        {
            _bus = new SimpleMessageBus();
        }

        [TestMethod]
        public void It_publishes_messages_to_subscribed_handlers_only()
        {
            // arrange
            var aMessage = new MessageA();
            var aHandler = new Mock<IHandles<MessageA>>();
            var bHandler = new Mock<IHandles<MessageB>>();
            _bus.Subscribe(aHandler.Object);
            _bus.Subscribe(bHandler.Object);

            // act
            _bus.Publish(aMessage);

            // assert
            aHandler.Verify(x => x.Handle(It.Is<MessageA>(m => ReferenceEquals(m, aMessage))), Times.Once);
            aHandler.Verify(x => x.Handle(It.IsAny<MessageA>()), Times.Once);
            bHandler.Verify(x => x.Handle(It.IsAny<MessageB>()), Times.Never);
        }

        [TestMethod]
        public void It_does_not_publish_messages_to_unsubscribed_handlers()
        {
            // arrange
            var aMessage = new MessageA();
            var a1Handler = new Mock<IHandles<MessageA>>();
            var a2Handler = new Mock<IHandles<MessageA>>();
            _bus.Subscribe(a1Handler.Object);
            _bus.Subscribe(a2Handler.Object);

            // act
            _bus.UnSubscribe(a1Handler.Object);
            _bus.Publish(aMessage);

            // assert
            a1Handler.Verify(x => x.Handle(It.IsAny<MessageA>()), Times.Never);
            a2Handler.Verify(x => x.Handle(It.Is<MessageA>(m => ReferenceEquals(m, aMessage))), Times.Once);
        }
    }

    public class MessageA
    {
    }

    public class MessageB
    {
    }
}