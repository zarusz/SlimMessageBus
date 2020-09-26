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

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaConsumerProcessorTest : IDisposable
    {
        private readonly TopicPartition _topicPartition;
        private readonly ILoggerFactory _loggerFactory;

        private readonly Mock<IKafkaCommitController> _commitControllerMock = new Mock<IKafkaCommitController>();
        private readonly Mock<ICheckpointTrigger> _checkpointTrigger = new Mock<ICheckpointTrigger>();
        private readonly Mock<MessageQueueWorker<Message>> _messageQueueWorkerMock;

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

            static byte[] MessageValueProvider(Message m) => m.Value;
            var consumerInstancePoolMock = new Mock<ConsumerInstancePoolMessageProcessor<Message>>(consumerSettings, massageBusMock.Bus, (Func<Message, byte[]>)MessageValueProvider, null);
            _messageQueueWorkerMock = new Mock<MessageQueueWorker<Message>>(consumerInstancePoolMock.Object, _checkpointTrigger.Object, _loggerFactory);
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
        public void WhenOnPartitionEndReachedThenShouldCommit()
        {
            // arrange
            var partition = new TopicPartitionOffset(_topicPartition, new Offset(10));

            // act
            _subject.OnPartitionEndReached(partition).Wait();

            // assert
            _commitControllerMock.Verify(x => x.Commit(partition), Times.Once);
        }

        [Fact]
        public void WhenOnPartitionRevokedThenShouldClearWorkerQueue()
        {
            // arrange

            // act
            _subject.OnPartitionRevoked().Wait();

            // assert
            _messageQueueWorkerMock.Verify(x => x.Clear(), Times.Once);
        }

        [Fact]
        public void WhenOnMessageAndWorkerQueueSubmitReturnTrueThenShouldCommit()
        {
            // arrange
            var message = GetSomeMessage();
            _messageQueueWorkerMock.Setup(x => x.Submit(message)).Returns(true);

            // act
            _subject.OnMessage(message).Wait();

            // assert
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset), Times.Once);
        }

        [Fact]
        public void WhenOnMessageAndWorkerQueueSubmitReturnFalseThenShouldNotCommit()
        {
            // arrange
            var message = GetSomeMessage();
            _messageQueueWorkerMock.Setup(x => x.Submit(message)).Returns(false);

            // act
            _subject.OnMessage(message).Wait();

            // assert
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset), Times.Never);
        }

        [Fact]
        public void WhenCommitThenShouldSyncPendingMessages()
        {
            // arrange
            var offset = new TopicPartitionOffset(_topicPartition, new Offset(10));

            // act
            _subject.Commit(offset).Wait();

            // assert
            _messageQueueWorkerMock.Verify(x => x.WaitAll(), Times.Once);
            _commitControllerMock.Verify(x => x.Commit(offset), Times.Once);
        }

        private Message GetSomeMessage()
        {
            return new Message(_topicPartition.Topic, _topicPartition.Partition, 10, new byte[] { 10, 20 }, new byte[] { 10, 20 }, new Timestamp(), null);
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
