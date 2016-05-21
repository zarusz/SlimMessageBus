using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace SlimMessageBus.Test
{
    /// <summary>
    /// Unit test for <see cref="MessageBus"/>
    /// </summary>
    [TestClass]
    public class MessageBusTest
    {
        [TestMethod]
        public void It_calls_provider_to_determine_current_bus()
        {
            // arrange
            var busMock = new Mock<IMessageBus>();
            var providerMock = new Mock<Func<IMessageBus>>();
            providerMock.Setup(x => x()).Returns(busMock.Object);
            MessageBus.SetProvider(providerMock.Object);

            // act
            var currentBus = MessageBus.Current;

            // assert
            Assert.AreSame(busMock.Object, currentBus);
            providerMock.Verify(x => x(), Times.Once);
        }
    }
}
