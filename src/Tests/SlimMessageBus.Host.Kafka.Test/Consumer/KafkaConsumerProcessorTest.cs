using Confluent.Kafka;
using FluentAssertions;
using Moq;
using SlimMessageBus.Host.Config;
using System;
using System.Threading.Tasks;
using Xunit;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaConsumerProcessorTest
    {
        MessageBusMock _mssageBusMock;
        TopicPartition _topicPartition;

        Mock<IKafkaCommitController> _commitControllerMock = new Mock<IKafkaCommitController>();
        Mock<ICheckpointTrigger> _checkpointTrigger = new Mock<ICheckpointTrigger>();
        Mock<ConsumerInstancePool<Message>> consumerInstancePoolMock;
        Mock<MessageQueueWorker<Message>> _messageQueueWorkerMock;

        ConsumerSettings _consumerSettings;
        SomeMessageConsumer _consumer = new SomeMessageConsumer();

        KafkaConsumerProcessor _subject;

        public KafkaConsumerProcessorTest()
        {
            _topicPartition = new TopicPartition("topic-a", 0);

            _consumerSettings = new ConsumerSettings()
            {
                MessageType = typeof(SomeMessage),
                Topic = _topicPartition.Topic,
                ConsumerType = typeof(SomeMessageConsumer),
                ConsumerMode = ConsumerMode.Subscriber,
            };

            _mssageBusMock = new MessageBusMock();
            _mssageBusMock.BusSettings.Consumers.Add(_consumerSettings);
            _mssageBusMock.DependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageConsumer))).Returns(_consumer);

            Func<Message, byte[]> messageValueProvider = m => m.Value;
            consumerInstancePoolMock = new Mock<ConsumerInstancePool<Message>>(_consumerSettings, _mssageBusMock.Object, messageValueProvider, null);
            _messageQueueWorkerMock = new Mock<MessageQueueWorker<Message>>(consumerInstancePoolMock.Object, _checkpointTrigger.Object);
            _subject = new KafkaConsumerProcessor(_consumerSettings, _topicPartition, _commitControllerMock.Object, _mssageBusMock.Object, _messageQueueWorkerMock.Object);
        }

        [Fact]
        public void AfterCreation_TopicPartitonSet()
        {
            _subject.TopicPartition.ShouldBeEquivalentTo(_topicPartition);
        }

        [Fact]
        public void OnPartitionEndReached_ShouldCommit()
        {
            // arrange
            var partition = new TopicPartitionOffset(_topicPartition, new Offset(10));

            // act
            _subject.OnPartitionEndReached(partition).Wait();

            // assert
            _commitControllerMock.Verify(x => x.Commit(partition), Times.Once);
        }

        [Fact]
        public void OnPartitionRevoked_ShouldClearWorkerQueue()
        {
            // arrange

            // act
            _subject.OnPartitionRevoked().Wait();

            // assert
            _messageQueueWorkerMock.Verify(x => x.Clear(), Times.Once);
        }

        [Fact]
        public void OnMessage_WhenWorkerQueueSubmitReturnTrue_ShouldCommit()
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
        public void OnMessage_WhenWorkerQueueSubmitReturnFalse_ShouldNotCommit()
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
        public void Commit_ShouldSyncPendingMessages()
        {
            // arrange
            var offset = new TopicPartitionOffset(_topicPartition, new Offset(10));
            Message message;
            //_messageQueueWorkerMock.Setup(x => x.Submit(message)).Returns(false);

            // act
            _subject.Commit(offset).Wait();

            // assert
            _messageQueueWorkerMock.Verify(x => x.WaitAll(out message), Times.Once);
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
        public Task OnHandle(SomeMessage message, string topic)
        {
            return Task.FromResult(0);
        }
    }
}
