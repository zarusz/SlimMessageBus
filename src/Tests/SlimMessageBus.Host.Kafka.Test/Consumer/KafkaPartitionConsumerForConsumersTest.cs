﻿namespace SlimMessageBus.Host.Kafka.Test;

using ConsumeResult = ConsumeResult<Ignore, byte[]>;

public class KafkaPartitionConsumerForConsumersTest : IDisposable
{
    private readonly TopicPartition _topicPartition;
    private readonly ILoggerFactory _loggerFactory;

    private readonly Mock<IKafkaCommitController> _commitControllerMock = new();

    private readonly ConsumerBuilder<SomeMessage> _consumerBuilder;

    private readonly SomeMessageConsumer _consumer = new();

    private readonly Lazy<KafkaPartitionConsumerForConsumers> _subject;

    public KafkaPartitionConsumerForConsumersTest()
    {
        _loggerFactory = NullLoggerFactory.Instance;

        _topicPartition = new TopicPartition("topic-a", 0);
        var group = "group-a";

        _consumerBuilder = new ConsumerBuilder<SomeMessage>(new MessageBusSettings());
        _consumerBuilder.Path(_topicPartition.Topic);
        _consumerBuilder.WithConsumer<SomeMessageConsumer>();
        _consumerBuilder.KafkaGroup(group);

        var massageBusMock = new MessageBusMock();
        massageBusMock.BusSettings.Consumers.Add(_consumerBuilder.ConsumerSettings);
        massageBusMock.ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(SomeMessageConsumer))).Returns(_consumer);
        massageBusMock.ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(ILoggerFactory))).Returns(_loggerFactory);
        massageBusMock.ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        massageBusMock.ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), TimeProvider.System, NullLoggerFactory.Instance));

        var headerSerializer = new StringValueSerializer();
        var messageProviderMock = new Mock<MessageProvider<ConsumeResult>>();

        _subject = new Lazy<KafkaPartitionConsumerForConsumers>(() => new KafkaPartitionConsumerForConsumers(massageBusMock.Bus.LoggerFactory,
                                                                                                             [_consumerBuilder.ConsumerSettings],
                                                                                                             group,
                                                                                                             _topicPartition,
                                                                                                             _commitControllerMock.Object,
                                                                                                             headerSerializer,
                                                                                                             messageProviderMock.Object,
                                                                                                             massageBusMock.Bus));
    }

    public void Dispose()
    {
        _subject?.Value.Dispose();
    }

    [Fact]
    public void When_NewInstance_Then_TopicPartitionSet()
    {
        _subject.Value.TopicPartition.Should().Be(_topicPartition);
    }

    [Fact]
    public async Task When_OnPartitionEndReached_Then_ShouldCommit()
    {
        // arrange
        var message = GetSomeMessage();
        _subject.Value.OnPartitionAssigned(message.TopicPartition);
        await _subject.Value.OnMessage(message);

        // act
        _subject.Value.OnPartitionEndReached();

        // assert
        _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset.AddOffset(1)), Times.Once);
    }

    [Fact]
    public async Task When_OnPartitionRevoked_Then_ShouldNeverCommit()
    {
        // arrange
        var message = GetSomeMessage();
        _subject.Value.OnPartitionAssigned(message.TopicPartition);
        await _subject.Value.OnMessage(message);

        // act
        _subject.Value.OnPartitionRevoked();

        // assert
        _commitControllerMock.Verify(x => x.Commit(It.IsAny<TopicPartitionOffset>()), Times.Never);
    }

    [Fact]
    public async Task When_OnMessage_Given_CheckpointTriggerFires_Then_ShouldCommit()
    {
        // arrange
        _consumerBuilder.CheckpointEvery(3);
        _consumerBuilder.CheckpointAfter(TimeSpan.FromSeconds(60));

        var message1 = GetSomeMessage(offsetAdd: 0);
        var message2 = GetSomeMessage(offsetAdd: 1);
        var message3 = GetSomeMessage(offsetAdd: 2);

        _subject.Value.OnPartitionAssigned(message1.TopicPartition);

        // act
        await _subject.Value.OnMessage(message1);
        await _subject.Value.OnMessage(message2);
        await _subject.Value.OnMessage(message3);

        // assert
        _commitControllerMock.Verify(x => x.Commit(message3.TopicPartitionOffset.AddOffset(1)), Times.Once);
    }

    private ConsumeResult GetSomeMessage(int offsetAdd = 0)
    {
        return new ConsumeResult
        {
            Topic = _topicPartition.Topic,
            Partition = _topicPartition.Partition,
            Offset = 10 + offsetAdd,
            Message = new Message<Ignore, byte[]> { Key = null, Value = [10, 20] },
            IsPartitionEOF = false,
        };
    }
}

public class SomeMessage
{
}

public class SomeMessageConsumer : IConsumer<SomeMessage>
{
    public Task OnHandle(SomeMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
