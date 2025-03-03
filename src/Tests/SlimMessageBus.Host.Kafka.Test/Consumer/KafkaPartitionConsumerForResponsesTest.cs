namespace SlimMessageBus.Host.Kafka.Test;

using System.Text;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

public class KafkaPartitionConsumerForResponsesTest : IDisposable
{
    private readonly MessageBusMock _messageBusMock;
    private readonly TopicPartition _topicPartition;
    private readonly Mock<IKafkaCommitController> _commitControllerMock = new();
    private readonly Mock<ICheckpointTrigger> _checkpointTrigger = new();
    private KafkaPartitionConsumerForResponses _subject;
    private readonly Mock<MessageProvider<ConsumeResult>> _messageProvider = new();
    private readonly Mock<IPendingRequestStore> _pendingRequestStore = new();

    public KafkaPartitionConsumerForResponsesTest()
    {
        _topicPartition = new TopicPartition("topic-a", 0);

        var requestResponseSettings = new RequestResponseSettings
        {
            Path = "topic-a"
        };
        requestResponseSettings.SetGroup("group-a");

        _messageBusMock = new MessageBusMock
        {
            BusSettings =
            {
                RequestResponse = requestResponseSettings
            }
        };

        _subject = new KafkaPartitionConsumerForResponses(_messageBusMock.Bus.LoggerFactory,
                                                          requestResponseSettings,
                                                          requestResponseSettings.GetGroup(),
                                                          _topicPartition,
                                                          _commitControllerMock.Object,
                                                          _messageProvider.Object,
                                                          _pendingRequestStore.Object,
                                                          _messageBusMock.TimeProvider,
                                                          new DefaultKafkaHeaderSerializer())
        {
            CheckpointTrigger = _checkpointTrigger.Object
        };
    }

    [Fact]
    public void When_NewInstance_Then_TopicPartitionSet()
    {
        _subject.TopicPartition.Should().Be(_topicPartition);
    }

    [Fact]
    public async Task When_OnPartitionEndReached_Then_ShouldCommit()
    {
        // arrange
        var messageOffset = new TopicPartitionOffset(_topicPartition, new Offset(10));
        var message = GetSomeMessage();

        _subject.OnPartitionAssigned(_topicPartition);
        await _subject.OnMessage(message);

        // act
        _subject.OnPartitionEndReached();

        // assert
        _commitControllerMock.Verify(x => x.Commit(messageOffset.AddOffset(1)), Times.Once);
    }

    [Fact]
    public void When_OnPartitionAssigned_Then_ShouldResetTrigger()
    {
        // arrange

        // act
        _subject.OnPartitionAssigned(_topicPartition);

        // assert
        _checkpointTrigger.Verify(x => x.Reset(), Times.Once);
    }

    [Fact]
    public async Task When_OnMessage_Given_SuccessMessage_ThenOnResponseArrived()
    {
        // arrange
        var requestId = "1";
        var request = new SomeMessage();
        var responseTransportMessage = GetSomeMessage();
        responseTransportMessage.Message.Headers.Add(ReqRespMessageHeaders.RequestId, Encoding.UTF8.GetBytes(requestId));
        var response = new SomeMessage();

        _subject.OnPartitionAssigned(responseTransportMessage.TopicPartition);

        var pendingRequestState = new PendingRequestState(requestId, request, request.GetType(), response.GetType(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), default);
        _pendingRequestStore.Setup(x => x.GetById(requestId)).Returns(pendingRequestState);

        _messageProvider.Setup(x => x(response.GetType(), responseTransportMessage)).Returns(response);

        // act
        await _subject.OnMessage(responseTransportMessage);

        // assert
        pendingRequestState.TaskCompletionSource.Task.IsCompleted.Should().BeTrue();
        var result = await pendingRequestState.TaskCompletionSource.Task;
        result.Should().Be(response);
    }

    [Fact]
    public async Task When_OnMessage_Given_CheckpointReturnTrue_Then_ShouldCommit()
    {
        // arrange
        _checkpointTrigger.Setup(x => x.Increment()).Returns(true);
        var message = GetSomeMessage();

        _subject.OnPartitionAssigned(message.TopicPartition);

        // act
        await _subject.OnMessage(message);

        // assert
        _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset.AddOffset(1)), Times.Once);
    }

    [Fact]
    public async Task When_OnMessage_Given_WhenCheckpointReturnFalse_Then_ShouldNotCommit()
    {
        // arrange
        _checkpointTrigger.Setup(x => x.Increment()).Returns(false);
        var message = GetSomeMessage();

        _subject.OnPartitionAssigned(message.TopicPartition);

        // act
        await _subject.OnMessage(message);

        // assert
        _commitControllerMock.Verify(x => x.Commit(It.IsAny<TopicPartitionOffset>()), Times.Never);
    }

    private ConsumeResult GetSomeMessage()
    {
        return new ConsumeResult
        {
            Topic = _topicPartition.Topic,
            Partition = _topicPartition.Partition,
            Offset = 10,
            Message = new Message<Ignore, byte[]>
            {
                Key = null,
                Value = [10, 20],
                Headers = new Headers
                {
                    { "test-header", Encoding.UTF8.GetBytes("test-value") }
                }
            },
            IsPartitionEOF = false,
        };
    }

    #region IDisposable

    public void Dispose()
    {
        _subject?.Dispose();
        _subject = null;
    }

    #endregion
}

