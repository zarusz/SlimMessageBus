namespace SlimMessageBus.Host.Test.Consumer
{
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using Xunit;

    public class ConcurrencyIncreasingMessageProcessorDecoratorTest
    {
        private readonly MessageBusMock _busMock;
        private readonly Mock<IMessageProcessor<SomeMessage>> _messageProcessorMock;
        private ConcurrencyIncreasingMessageProcessorDecorator<SomeMessage> _subject;

        public ConcurrencyIncreasingMessageProcessorDecoratorTest()
        {
            _busMock = new MessageBusMock();           
            _messageProcessorMock = new Mock<IMessageProcessor<SomeMessage>>();
        }

        [Theory]
        [InlineData(10, 40)]
        [InlineData(2, 40)]
        [InlineData(1, 40)]
        public async Task WhenProcessMessageGivenNMessagesAndConcurrencySetToCThenNMethodInvokationsHappenOnTargetWithCConcurrently(int concurrency, int expectedMessageCount)
        {
            // arrange
            var consumerSettings = new ConsumerBuilder<SomeMessage>(new MessageBusSettings()).Topic(null).WithConsumer<IConsumer<SomeMessage>>().Instances(concurrency).ConsumerSettings;
            _subject = new ConcurrencyIncreasingMessageProcessorDecorator<SomeMessage>(consumerSettings, _busMock.Bus, _messageProcessorMock.Object);

            var currentSectionCount = 0;
            var maxSectionCount = 0;
            var maxSectionCountLock = new object();
            var messageCount = 0;

            _messageProcessorMock.Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IMessageTypeConsumerInvokerSettings>())).Returns(async () =>
            {
                // Entering critical section
                Interlocked.Increment(ref currentSectionCount);

                // Simulate work
                await Task.Delay(50);

                Interlocked.Increment(ref messageCount);

                lock (maxSectionCountLock)
                {
                    if (currentSectionCount > maxSectionCount)
                    {
                        maxSectionCount = currentSectionCount;
                    }
                }

                // Simulate work
                await Task.Delay(50);

                // Leaving critical section
                Interlocked.Decrement(ref currentSectionCount);
                return null;
            });

            // act
            var msg = new SomeMessage();
            for (var i = 0; i < expectedMessageCount; i++)
            {
                // executed in sequence
                await _subject.ProcessMessage(msg, null);
            }

            // assert
            while (_subject.PendingCount > 0)
            {
                await Task.Delay(100);
            }

            messageCount.Should().Be(expectedMessageCount);
            maxSectionCount.Should().Be(concurrency);
        }
    }
}
