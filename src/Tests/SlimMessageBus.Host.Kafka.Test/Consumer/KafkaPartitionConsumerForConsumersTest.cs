namespace SlimMessageBus.Host.Kafka.Test
{
    using Confluent.Kafka;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using System;
    using System.Threading.Tasks;
    using Xunit;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

    public class KafkaPartitionConsumerForConsumersTest : IDisposable
    {
        private readonly TopicPartition _topicPartition;
        private readonly ILoggerFactory _loggerFactory;

        private readonly Mock<IKafkaCommitController> _commitControllerMock = new Mock<IKafkaCommitController>();

        private readonly ConsumerBuilder<SomeMessage> _consumerBuilder;

        private readonly SomeMessageConsumer _consumer = new SomeMessageConsumer();

        private readonly Lazy<KafkaPartitionConsumerForConsumers> _subject;

        public KafkaPartitionConsumerForConsumersTest()
        {
            _loggerFactory = NullLoggerFactory.Instance;

            _topicPartition = new TopicPartition("topic-a", 0);

            _consumerBuilder = new ConsumerBuilder<SomeMessage>(new MessageBusSettings());
            _consumerBuilder.Path(_topicPartition.Topic);
            _consumerBuilder.WithConsumer<SomeMessageConsumer>();
            _consumerBuilder.KafkaGroup("group-a");

            var massageBusMock = new MessageBusMock();
            massageBusMock.BusSettings.Consumers.Add(_consumerBuilder.ConsumerSettings);
            massageBusMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageConsumer))).Returns(_consumer);
            massageBusMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(ILoggerFactory))).Returns(_loggerFactory);

            var headerSerializer = new StringValueSerializer();
            MessageWithHeaders MessageValueProvider(ConsumeResult m) => m.ToMessageWithHeaders(headerSerializer);

            var consumerInstancePoolMock = new Mock<ConsumerInstanceMessageProcessor<ConsumeResult>>(_consumerBuilder.ConsumerSettings, massageBusMock.Bus, (Func<ConsumeResult, MessageWithHeaders>)MessageValueProvider, null);
            _subject = new Lazy<KafkaPartitionConsumerForConsumers>(() => new KafkaPartitionConsumerForConsumers(_consumerBuilder.ConsumerSettings, _topicPartition, _commitControllerMock.Object, massageBusMock.Bus, headerSerializer));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _subject.Value.Dispose();
            }
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
            await _subject.Value.OnMessage(message);

            // act
            _subject.Value.OnPartitionEndReached(message.TopicPartitionOffset);

            // assert
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset), Times.Once);
        }

        [Fact]
        public async Task When_OnPartitionRevoked_Then_ShouldNeverCommit()
        {
            // arrange
            var message = GetSomeMessage();
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

            // act
            await _subject.Value.OnMessage(message1);
            await _subject.Value.OnMessage(message2);
            await _subject.Value.OnMessage(message3);

            // assert
            _commitControllerMock.Verify(x => x.Commit(message3.TopicPartitionOffset), Times.Once);
        }

        private ConsumeResult GetSomeMessage(int offsetAdd = 0)
        {
            return new ConsumeResult
            {
                Topic = _topicPartition.Topic,
                Partition = _topicPartition.Partition,
                Offset = 10 + offsetAdd,
                Message = new Message<Ignore, byte[]> { Key = null, Value = new byte[] { 10, 20 } },
                IsPartitionEOF = false,
            };
        }
    }

    public class SomeMessage
    {

    }

    public class SomeMessageConsumer : IConsumer<SomeMessage>
    {
        public Task OnHandle(SomeMessage message, string name)
        {
            return Task.CompletedTask;
        }
    }
}
