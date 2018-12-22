using Confluent.Kafka;
using FluentAssertions;
using Moq;
using SlimMessageBus.Host.Config;
using System;
using SlimMessageBus.Host.Kafka.Configs;
using Xunit;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaResponseProcessorTest : IDisposable
    {
        private readonly MessageBusMock _messageBusMock;
        private readonly TopicPartition _topicPartition;
        private readonly Mock<IKafkaCommitController> _commitControllerMock = new Mock<IKafkaCommitController>();
        private readonly Mock<ICheckpointTrigger> _checkpointTrigger = new Mock<ICheckpointTrigger>();
        private readonly KafkaResponseProcessor _subject;

        public KafkaResponseProcessorTest()
        {
            _topicPartition = new TopicPartition("topic-a", 0);

            var requestResponseSettings = new RequestResponseSettings
            {
                Topic = "topic-a"
            };
            requestResponseSettings.SetGroup("group-a");

            _messageBusMock = new MessageBusMock
            {
                BusSettings =
                {
                    RequestResponse = requestResponseSettings
                }
            };

            _subject = new KafkaResponseProcessor(_messageBusMock.BusSettings.RequestResponse, _topicPartition, _commitControllerMock.Object, _messageBusMock.Bus, _checkpointTrigger.Object);
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
        public void WhenOnPartitionRevokedThenShouldResetTrigger()
        {
            // arrange

            // act
            _subject.OnPartitionRevoked().Wait();

            // assert
            _checkpointTrigger.Verify(x => x.Reset(), Times.Once);
        }

        [Fact]
        public void GivenSuccessMessageWhenOnMessageThenOnResponseArrived()
        {
            // arrange
            var message = GetSomeMessage();

            // act
            _subject.OnMessage(message).Wait();

            // assert
            _messageBusMock.BusMock.Verify(x => x.OnResponseArrived(message.Value, message.Topic), Times.Once);
        }

        [Fact]
        public void GivenMessageErrorsWhenOnMessageThenShouldCallHook()
        {
            // arrange
            var message = GetSomeMessage();
            var onResponseMessageFaultMock = new Mock<Action<RequestResponseSettings, object, Exception>>();
            _messageBusMock.BusSettings.RequestResponse.OnResponseMessageFault = onResponseMessageFaultMock.Object;
            var e = new Exception();
            _messageBusMock.BusMock.Setup(x => x.OnResponseArrived(message.Value, message.Topic)).Throws(e);

            // act
            _subject.OnMessage(message).Wait();

            // assert
            onResponseMessageFaultMock.Verify(x => x(_messageBusMock.BusSettings.RequestResponse, message, e), Times.Once);
            
        }

        [Fact]
        public void GivenCheckpointReturnTrueWhenOnMessageThenShouldCommit()
        {
            // arrange
            _checkpointTrigger.Setup(x => x.Increment()).Returns(true);
            var message = GetSomeMessage();

            // act
            _subject.OnMessage(message).Wait();

            // assert
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset), Times.Once);
        }

        [Fact]
        public void GivenWhenCheckpointReturnFalseWhenOnMessageThenShouldNotCommit()
        {
            // arrange
            _checkpointTrigger.Setup(x => x.Increment()).Returns(false);
            var message = GetSomeMessage();

            // act
            _subject.OnMessage(message).Wait();

            // assert
            _commitControllerMock.Verify(x => x.Commit(It.IsAny<TopicPartitionOffset>()), Times.Never);
        }

        private Message GetSomeMessage()
        {
            return new Message(_topicPartition.Topic, _topicPartition.Partition, 10, new byte[] { 10, 20 }, new byte[] { 10, 20 }, new Timestamp(), null);
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
    }
}
