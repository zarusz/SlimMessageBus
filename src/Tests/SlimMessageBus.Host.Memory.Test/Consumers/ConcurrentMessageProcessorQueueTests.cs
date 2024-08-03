namespace SlimMessageBus.Host.Memory.Test.Consumers;

using Microsoft.Extensions.Logging.Abstractions;

public class ConcurrentMessageProcessorQueueTests
{
    [Fact]
    public async Task When_Enqueue_Given_FourMessagesEnqueued_Then_ProcessMessageIsCalledOnFirstTwoThenTwoAfterThat()
    {
        // Arrange
        var messageProcessor = new Mock<IMessageProcessor<object>>();
        var cancellationToken = new CancellationToken();
        var sut = new ConcurrentMessageProcessorQueue(messageProcessor.Object, NullLogger<ConcurrentMessageProcessorQueue>.Instance, concurrency: 2, cancellationToken);
        var transportMessage1 = new object();
        var transportMessage2 = new object();
        var transportMessage3 = new object();
        var transportMessage4 = new object();
        var messageHeaders = new Dictionary<string, object>();

        static async Task<ProcessMessageResult> ProcessMessageFake(object transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties, IServiceProvider currentServiceProvider, CancellationToken cancellationToken)
        {
            await Task.Delay(500, cancellationToken);
            return new ProcessMessageResult(null, null, null);
        }

        messageProcessor
            .Setup(x => x.ProcessMessage(It.IsAny<object>(), messageHeaders, It.IsAny<IDictionary<string, object>>(), null, cancellationToken))
            .Returns(ProcessMessageFake);

        // Act
        sut.Enqueue(transportMessage1, messageHeaders);
        sut.Enqueue(transportMessage2, messageHeaders);
        sut.Enqueue(transportMessage3, messageHeaders);
        sut.Enqueue(transportMessage4, messageHeaders);

        // Assert

        // Executed right away to create Task
        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage1, messageHeaders, It.Is<IDictionary<string, object>>(a => a.ContainsKey(MemoryMessageBusProperties.CreateScope)), null, cancellationToken), Times.Once);
        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage2, messageHeaders, It.Is<IDictionary<string, object>>(a => a.ContainsKey(MemoryMessageBusProperties.CreateScope)), null, cancellationToken), Times.Once);

        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage3, messageHeaders, It.IsAny<IDictionary<string, object>>(), null, cancellationToken), Times.Never);
        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage4, messageHeaders, It.IsAny<IDictionary<string, object>>(), null, cancellationToken), Times.Never);

        // Wait for the 1st message to be processed
        await Task.Delay(600);

        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage3, messageHeaders, It.Is<IDictionary<string, object>>(a => a.ContainsKey(MemoryMessageBusProperties.CreateScope)), null, cancellationToken), Times.Once);
        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage4, messageHeaders, It.Is<IDictionary<string, object>>(a => a.ContainsKey(MemoryMessageBusProperties.CreateScope)), null, cancellationToken), Times.Once);
    }
}

