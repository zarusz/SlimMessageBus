namespace SlimMessageBus.Host.Kafka.Test;

public class KafkaGroupConsumerTests
{
    private readonly Mock<ILogger<KafkaGroupConsumer>> _loggerMock = new();
    private readonly KafkaGroupConsumer _subject;

    public KafkaGroupConsumerTests()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

        var processorFactoryMock = new Mock<Func<TopicPartition, IKafkaCommitController, IKafkaPartitionConsumer>>();

        var providerSettings = new KafkaMessageBusSettings("host");
        var consumerSettings = Array.Empty<AbstractConsumerSettings>();

        var subjectMock = new Mock<KafkaGroupConsumer>(loggerFactoryMock.Object, providerSettings, consumerSettings, Array.Empty<IAbstractConsumerInterceptor>(), "group", new List<string> { "topic" }, processorFactoryMock.Object) { CallBase = true };
        _subject = subjectMock.Object;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_OnOffsetsCommitted_Given_Succeeded_Then_ShouldLog(bool debugEnabled)
    {
        // arrangen
        _loggerMock.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(debugEnabled);

        var tpo = new TopicPartitionOffset("topic", 0, 10);
        var committedOffset = new CommittedOffsets([new TopicPartitionOffsetError(tpo, null)], new Error(ErrorCode.NoError));

        // act
        _subject.OnOffsetsCommitted(committedOffset);

        // assert

        _loggerMock.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        _loggerMock.Verify(x => x.IsEnabled(LogLevel.Debug), Times.Once);
        if (debugEnabled)
        {
            _loggerMock.Verify(x => x.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
        _loggerMock.VerifyNoOtherCalls();
    }

}