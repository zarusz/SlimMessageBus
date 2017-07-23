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
        public void CurrentAccess_CallsProviderForInstance()
        {
            // arrange
            var busMock = new Mock<IMessageBus>();
            var providerMock = new Mock<Func<IMessageBus>>();
            providerMock.Setup(x => x()).Returns(busMock.Object);
            MessageBus.SetProvider(providerMock.Object);

            // act
            var currentBus1 = MessageBus.Current;
            var currentBus2 = MessageBus.Current;

            // assert
            Assert.AreSame(busMock.Object, currentBus1);
            Assert.AreSame(busMock.Object, currentBus2);
            providerMock.Verify(x => x(), Times.Exactly(2));
        }
    }
}
