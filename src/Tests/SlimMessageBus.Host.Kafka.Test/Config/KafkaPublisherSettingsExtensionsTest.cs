using FluentAssertions;
using SlimMessageBus.Host.Config;
using System;
using Xunit;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaPublisherSettingsExtensionsTest
    {
        [Fact]
        public void GetKeyProvider_ReturnsNull_ByDefault()
        {
            // arrange
            var ps = new PublisherSettings();

            // act
            var keyProvider = ps.GetKeyProvider();

            // assert
            keyProvider.Should().BeNull();
        }

        [Fact]
        public void GetPartitionProvider_ReturnsNull_ByDefault()
        {
            // arrange
            var ps = new PublisherSettings();

            // act
            var partitionProvider = ps.GetPartitionProvider();

            // assert
            partitionProvider.Should().BeNull();
        }
    }
}
