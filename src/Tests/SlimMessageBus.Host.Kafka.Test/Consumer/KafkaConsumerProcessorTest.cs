using Confluent.Kafka;
using FluentAssertions;
using Moq;
using SlimMessageBus.Host.Config;
using System;
using System.Threading.Tasks;
using SlimMessageBus.Host.Kafka.Configs;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaConsumerProcessorTest : IDisposable
    {
        private readonly TopicPartition _topicPartition;
        private readonly ILoggerFactory _loggerFactory;

        private readonly Mock<IKafkaCommitController> _commitControllerMock = new Mock<IKafkaCommitController>();
        private readonly Mock<ICheckpointTrigger> _checkpointTrigger = new Mock<ICheckpointTrigger>();
        private readonly Mock<MessageQueueWorker<ConsumeResult>> _messageQueueWorkerMock;

        private readonly SomeMessageConsumer _consumer = new SomeMessageConsumer();

        private readonly KafkaConsumerProcessor _subject;

        public KafkaConsumerProcessorTest()
        {
            _loggerFactory = NullLoggerFactory.Instance;

            _topicPartition = new TopicPartition("topic-a", 0);

            var consumerSettings = new ConsumerSettings
            {
                MessageType = typeof(SomeMessage),
                Topic = _topicPartition.Topic,
                ConsumerType = typeof(SomeMessageConsumer),
                ConsumerMode = ConsumerMode.Consumer,
            };
            consumerSettings.SetGroup("group-a");

            var massageBusMock = new MessageBusMock();
            massageBusMock.BusSettings.Consumers.Add(consumerSettings);
            massageBusMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageConsumer))).Returns(_consumer);
            massageBusMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(ILoggerFactory))).Returns(_loggerFactory);

            static byte[] MessageValueProvider(ConsumeResult m) => m.Message.Value;
            var consumerInstancePoolMock = new Mock<ConsumerInstancePoolMessageProcessor<ConsumeResult>>(consumerSettings, massageBusMock.Bus, (Func<ConsumeResult, byte[]>)MessageValueProvider, null);
            _messageQueueWorkerMock = new Mock<MessageQueueWorker<ConsumeResult>>(consumerInstancePoolMock.Object, _checkpointTrigger.Object, _loggerFactory);
            _subject = new KafkaConsumerProcessor(consumerSettings, _topicPartition, _commitControllerMock.Object, massageBusMock.Bus, _messageQueueWorkerMock.Object);
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
                _subject.Dispose();
            }
        }

        [Fact]
        public void WhenNewInstanceThenTopicPartitionSet()
        {
            _subject.TopicPartition.Should().Be(_topicPartition);
        }

        [Fact]
        public async Task WhenOnPartitionEndReachedThenShouldCommit()
        {
            // arrange
            var message = GetSomeMessage();
            _messageQueueWorkerMock.Setup(x => x.WaitAll()).ReturnsAsync(new MessageQueueResult<ConsumeResult> { Success = true, LastSuccessMessage = message });

            // act
            await _subject.OnPartitionEndReached(message.TopicPartitionOffset);

            // assert
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset.AddOffset(1)), Times.Once);
        }

        [Fact]
        public void WhenOnPartitionRevokedThenShouldClearWorkerQueue()
        {
            // arrange

            // act
            _subject.OnPartitionRevoked();

            // assert
            _messageQueueWorkerMock.Verify(x => x.Clear(), Times.Once);
        }

        [Fact]
        public async Task WhenOnMessageAndWorkerQueueSubmitReturnTrueThenShouldCommit()
        {
            // arrange
            var message = GetSomeMessage();
            _messageQueueWorkerMock.Setup(x => x.WaitAll()).ReturnsAsync(new MessageQueueResult<ConsumeResult> { Success = true, LastSuccessMessage = message });
            _messageQueueWorkerMock.Setup(x => x.Submit(message)).Returns(true);

            // act
            await _subject.OnMessage(message);

            // assert
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset.AddOffset(1)), Times.Once);
        }

        [Fact]
        public async Task WhenOnMessageAndWorkerQueueSubmitReturnFalseThenShouldNotCommit()
        {
            // arrange
            var message = GetSomeMessage();
            _messageQueueWorkerMock.Setup(x => x.Submit(message)).Returns(false);

            // act
            await _subject.OnMessage(message);

            // assert
            _commitControllerMock.Verify(x => x.Commit(It.IsAny<TopicPartitionOffset>()), Times.Never);
        }

        [Fact]
        public async Task WhenCommitThenShouldSyncPendingMessages()
        {
            // arrange
            var message = GetSomeMessage();
            _messageQueueWorkerMock.Setup(x => x.WaitAll()).ReturnsAsync(new MessageQueueResult<ConsumeResult> { Success = true, LastSuccessMessage = message });

            // act
            await _subject.Commit();

            // assert
            _messageQueueWorkerMock.Verify(x => x.WaitAll(), Times.Once);
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset.AddOffset(1)), Times.Once);
        }

        private ConsumeResult GetSomeMessage()
        {
            return new ConsumeResult
            {
                Topic = _topicPartition.Topic,
                Partition = _topicPartition.Partition,
                Offset = 10,
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
