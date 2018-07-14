using FluentAssertions;
using Moq;
using SlimMessageBus.Host.Config;
using System;
using Xunit;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaPublisherBuilderExtensionsTest
    {
        private readonly PublisherSettings _ps;
        private readonly PublisherBuilder<SomeMessage> _pb;

        public KafkaPublisherBuilderExtensionsTest()
        {
            _ps = new PublisherSettings();
            _pb = new PublisherBuilder<SomeMessage>(_ps);
        }

        [Fact]
        public void WhenKeyProviderThenCreatesUntypedWrapper()
        {
            // arrange
            var message = new SomeMessage();
            var messageKey = new byte[] { 1, 2 };

            var keyProviderMock = new Mock<Func<SomeMessage, string, byte[]>>();
            keyProviderMock.Setup(x => x(message, "topic1")).Returns(messageKey);

            // act
            _pb.KeyProvider(keyProviderMock.Object);

            // assert
            var keyProvider = _ps.GetKeyProvider();
            keyProvider(message, "topic1").Should().BeSameAs(messageKey);
            keyProviderMock.Verify(x => x(message, "topic1"), Times.Once);
        }

        [Fact]
        public void WhenPartitionProviderThenCreatesUntypedWrapper()
        {
            // arrange
            var message = new SomeMessage();

            var partitionProviderMock = new Mock<Func<SomeMessage, string, int>>();
            partitionProviderMock.Setup(x => x(message, "topic1")).Returns(1);

            // act
            _pb.PartitionProvider(partitionProviderMock.Object);

            // assert
            var partitionProvider = _ps.GetPartitionProvider();
            partitionProvider(message, "topic1").Should().Be(1);
            partitionProviderMock.Verify(x => x(message, "topic1"), Times.Once);
        }

        [Fact]
        public void WhenAttemptedNullKeyProviderThenThrowsConfigurationMessageBusException()
        {
            // arrange

            // act
            Action act = () => _pb.KeyProvider(null);

            // assert
            act.Should().Throw<ConfigurationMessageBusException>();
        }

        [Fact]
        public void WhenAttemptedNullPartitionProviderThenThrowsConfigurationMessageBusException()
        {
            // arrange

            // act
            Action act = () => _pb.PartitionProvider(null);

            // assert
            act.Should().Throw<ConfigurationMessageBusException>();
        }
    }
}
