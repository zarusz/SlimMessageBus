namespace SlimMessageBus.Host.Kafka.Test
{
    using Confluent.Kafka;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using System;
    using Xunit;

    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;
    using System.Threading.Tasks;
    using System.Text;
    using System.Collections.Generic;

    public class KafkaPartitionConsumerForResponsesTest : IDisposable
    {
        private readonly MessageBusMock _messageBusMock;
        private readonly TopicPartition _topicPartition;
        private readonly Mock<IKafkaCommitController> _commitControllerMock = new Mock<IKafkaCommitController>();
        private readonly Mock<ICheckpointTrigger> _checkpointTrigger = new Mock<ICheckpointTrigger>();
        private readonly KafkaPartitionConsumerForResponses _subject;

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

            _subject = new KafkaPartitionConsumerForResponses(_messageBusMock.BusSettings.RequestResponse, _topicPartition, _commitControllerMock.Object, _messageBusMock.Bus, _messageBusMock.SerializerMock.Object)
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
        public void When_OnPartitionEndReached_Then_ShouldCommit()
        {
            // arrange
            var partition = new TopicPartitionOffset(_topicPartition, new Offset(10));

            // act
            _subject.OnPartitionEndReached(partition);

            // assert
            _commitControllerMock.Verify(x => x.Commit(partition), Times.Once);
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
            var message = GetSomeMessage();

            // act
            await _subject.OnMessage(message);

            // assert
            _messageBusMock.BusMock.Verify(x => x.OnResponseArrived(message.Message.Value, message.Topic, It.Is<IDictionary<string, object>>(x => x.ContainsKey("test-header"))), Times.Once);
        }

        [Fact]
        public async Task When_OnMessage_Given_MessageErrors_Then_ShouldCallHook()
        {
            // arrange
            var message = GetSomeMessage();
            var onResponseMessageFaultMock = new Mock<Action<RequestResponseSettings, object, Exception>>();
            _messageBusMock.BusSettings.RequestResponse.OnResponseMessageFault = onResponseMessageFaultMock.Object;
            var e = new Exception();
            _messageBusMock.BusMock.Setup(x => x.OnResponseArrived(message.Message.Value, message.Topic, It.IsAny<IDictionary<string, object>>())).Throws(e);

            // act
            await _subject.OnMessage(message);

            // assert
            onResponseMessageFaultMock.Verify(x => x(_messageBusMock.BusSettings.RequestResponse, message, e), Times.Once);

        }

        [Fact]
        public async Task When_OnMessage_Given_CheckpointReturnTrue_Then_ShouldCommit()
        {
            // arrange
            _checkpointTrigger.Setup(x => x.Increment()).Returns(true);
            var message = GetSomeMessage();

            // act
            await _subject.OnMessage(message);

            // assert
            _commitControllerMock.Verify(x => x.Commit(message.TopicPartitionOffset), Times.Once);
        }

        [Fact]
        public async Task When_OnMessage_Given_WhenCheckpointReturnFalse_Then_ShouldNotCommit()
        {
            // arrange
            _checkpointTrigger.Setup(x => x.Increment()).Returns(false);
            var message = GetSomeMessage();

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
                    Value = new byte[] { 10, 20 },
                    Headers = new Headers
                    {
                        { "test-header", Encoding.UTF8.GetBytes("test-value") }
                    }
                },
                IsPartitionEOF = false,
            };
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
