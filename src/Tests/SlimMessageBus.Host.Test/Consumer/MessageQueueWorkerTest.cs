namespace SlimMessageBus.Host.Test.Consumer;

using Microsoft.Extensions.Logging.Abstractions;

public class MessageQueueWorkerTest
{
    private readonly Mock<IMessageProcessor<SomeMessage>> _consumerInstancePoolMock;
    private readonly Mock<ICheckpointTrigger> _checkpointTriggerMock;

    public MessageQueueWorkerTest()
    {
        _checkpointTriggerMock = new Mock<ICheckpointTrigger>();
        _consumerInstancePoolMock = new Mock<IMessageProcessor<SomeMessage>>();
    }

    [Fact]
    public void WhenCommitThenWaitsOnAllMessagesToComplete()
    {
        // arrange
        var w = new MessageQueueWorker<SomeMessage>(_consumerInstancePoolMock.Object, _checkpointTriggerMock.Object, NullLoggerFactory.Instance);

        var numFinishedMessages = 0;
        _consumerInstancePoolMock.Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<CancellationToken>(), It.IsAny<IServiceProvider>())).Returns(() => Task.Delay(50).ContinueWith(t => { Interlocked.Increment(ref numFinishedMessages); return ((Exception)null, (AbstractConsumerSettings)null, (object)null, (object)null); }, TaskScheduler.Current));

        const int numMessages = 100;
        for (var i = 0; i < numMessages; i++)
        {
            w.Submit(new SomeMessage(), new Dictionary<string, object>(), default);
        }

        // act
        var result = w.WaitAll().Result;

        // assert
        result.Success.Should().BeTrue();
        numFinishedMessages.Should().Be(numMessages);
    }

    [Fact]
    public void GivenIfSomeMessageFailsWhenCommitThenReturnsFirstNonFailedMessage()
    {
        // arrange
        var w = new MessageQueueWorker<SomeMessage>(_consumerInstancePoolMock.Object, _checkpointTriggerMock.Object, NullLoggerFactory.Instance);

        var taskQueue = new Queue<Task<Exception>>();
        taskQueue.Enqueue(Task.FromResult<Exception>(null));
        taskQueue.Enqueue(Task.Delay(3000).ContinueWith(x => (Exception)null));
        taskQueue.Enqueue(Task.FromException<Exception>(new Exception()));
        taskQueue.Enqueue(Task.FromResult<Exception>(null));
        taskQueue.Enqueue(Task.FromException<Exception>(new Exception()));
        taskQueue.Enqueue(Task.FromResult<Exception>(null));

        var messages = taskQueue.Select(x => new SomeMessage()).ToArray();

        _consumerInstancePoolMock.Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<CancellationToken>(), It.IsAny<IServiceProvider>())).Returns(async () => { var e = await taskQueue.Dequeue(); return (e, null, null, null); });

        foreach (var t in messages)
        {
            w.Submit(t, new Dictionary<string, object>(), default);
        }

        // act
        var result = w.WaitAll().Result;

        // assert
        result.Success.Should().BeFalse();
        result.LastSuccessMessage.Should().BeSameAs(messages[1]);
    }
}
