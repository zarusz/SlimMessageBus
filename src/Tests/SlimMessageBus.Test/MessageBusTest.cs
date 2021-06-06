namespace SlimMessageBus.Test
{
    using System;
    using Moq;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Unit test for <see cref="MessageBus"/>
    /// </summary>
    public class MessageBusTest
    {
        [Fact]
        public void When_Current_Given_ProviderSet_Then_CallsProviderForInstance()
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
            MessageBus.IsProviderSet().Should().BeTrue();
            currentBus1.Should().BeSameAs(busMock.Object);
            currentBus2.Should().BeSameAs(busMock.Object);
            providerMock.Verify(x => x(), Times.Exactly(2));
        }

        [Fact]
        public void When_Current_Given_ProviderNotSet_Then_ThrowsInvalidOperationException()
        {
            // arrange
            // provider not set

            // act 
            Action getCurrent = () =>
            {
                var mb = MessageBus.Current;
            };

            // assert
            getCurrent.Should().Throw<MessageBusException>();
            MessageBus.IsProviderSet().Should().BeFalse();
        }
    }
}
