namespace SlimMessageBus.Host.Memory.Test.Consumers;

using Microsoft.Extensions.Logging.Abstractions;

public class MessageProcessorQueueTests
{
    [Fact]
    public async void When_Enqueue_Given_TwoMessagesEnqueued_Then_ProcessMessageIsCalledOn1stMessageAndOn2ndAfterThat()
    {
        // Arrange
        var messageProcessor = new Mock<IMessageProcessor<object>>();
        var cancellationToken = new CancellationToken();
        var sut = new MessageProcessorQueue(messageProcessor.Object, NullLogger<MessageProcessorQueue>.Instance, cancellationToken);
        var transportMessage1 = new object();
        var transportMessage2 = new object();
        var messageHeaders = new Dictionary<string, object>();

        static async Task<ProcessMessageResult> ProcessMessageFake(object transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties, IServiceProvider currentServiceProvider, CancellationToken cancellationToken)
        {
            await Task.Delay(500, cancellationToken);
            return new ProcessMessageResult(null, null, null, null);
        }

        messageProcessor
            .Setup(x => x.ProcessMessage(It.IsAny<object>(), messageHeaders, It.IsAny<IDictionary<string, object>>(), null, cancellationToken))
            .Returns(ProcessMessageFake);

        // Act
        sut.Enqueue(transportMessage1, messageHeaders);
        sut.Enqueue(transportMessage2, messageHeaders);

        // Assert

        // Executed right away to create Task
        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage1, messageHeaders, It.IsAny<IDictionary<string, object>>(), null, cancellationToken), Times.Once);

        // Not yet, until the 1st message is processed
        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage2, messageHeaders, It.IsAny<IDictionary<string, object>>(), null, cancellationToken), Times.Never);

        // Wait for the 1st message to be processed
        await Task.Delay(600);

        messageProcessor
            .Verify(x => x.ProcessMessage(transportMessage2, messageHeaders, It.IsAny<IDictionary<string, object>>(), null, cancellationToken), Times.Once);
    }
}

