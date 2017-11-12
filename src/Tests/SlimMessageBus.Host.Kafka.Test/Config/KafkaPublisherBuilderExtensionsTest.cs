using FluentAssertions;
using Moq;
using SlimMessageBus.Host.Config;
using System;
using Xunit;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaPublisherBuilderExtensionsTest
    {
        private PublisherSettings ps;
        private PublisherBuilder<SomeMessage> pb;

        public KafkaPublisherBuilderExtensionsTest()
        {
            ps = new PublisherSettings();
            pb = new PublisherBuilder<SomeMessage>(ps);
        }

        [Fact]
        public void KeyProvider_CreatesUntypedWrapper()
        {
            // arrange
            var message = new SomeMessage();
            var messageKey = new byte[] { 1, 2 };

            var keyProviderMock = new Mock<Func<SomeMessage, string, byte[]>>();
            keyProviderMock.Setup(x => x(message, "topic1")).Returns(messageKey);

            // act
            pb.KeyProvider(keyProviderMock.Object);

            // assert
            var keyProvider = ps.GetKeyProvider();
            keyProvider(message, "topic1").Should().BeSameAs(messageKey);
            keyProviderMock.Verify(x => x(message, "topic1"), Times.Once);
        }

        [Fact]
        public void PartitionProvider_CreatesUntypedWrapper()
        {
            // arrange
            var message = new SomeMessage();

            var partitionProviderMock = new Mock<Func<SomeMessage, string, int>>();
            partitionProviderMock.Setup(x => x(message, "topic1")).Returns(1);

            // act
            pb.PartitionProvider(partitionProviderMock.Object);

            // assert
            var partitionProvider = ps.GetPartitionProvider();
            partitionProvider(message, "topic1").Should().Be(1);
            partitionProviderMock.Verify(x => x(message, "topic1"), Times.Once);
        }

        [Fact]
        public void KeyProvider_ThrowsConfigurationMessageBusException_WhenNullProviderSupplied()
        {
            // arrange

            // act
            Action act = () => pb.KeyProvider(null);

            // assert
            act.ShouldThrow<ConfigurationMessageBusException>();
        }

        [Fact]
        public void PartitionProvider_ThrowsConfigurationMessageBusException_WhenNullProviderSupplied()
        {
            // arrange

            // act
            Action act = () => pb.PartitionProvider(null);

            // assert
            act.ShouldThrow<ConfigurationMessageBusException>();
        }
    }
}
