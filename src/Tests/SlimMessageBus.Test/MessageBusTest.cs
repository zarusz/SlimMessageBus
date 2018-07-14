using System;
using Moq;
using FluentAssertions;
using Xunit;

namespace SlimMessageBus.Test
{
    /// <summary>
    /// Unit test for <see cref="MessageBus"/>
    /// </summary>
    public class MessageBusTest
    {
        [Fact]
        public void WhenCurrentThenCallsProviderForInstance()
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
            currentBus1.Should().BeSameAs(busMock.Object);
            currentBus2.Should().BeSameAs(busMock.Object);
            providerMock.Verify(x => x(), Times.Exactly(2));
        }
    }
}
