using FluentAssertions;
using SlimMessageBus.Host.Config;
using Xunit;

namespace SlimMessageBus.Host.Kafka.Test
{
    public class KafkaPublisherSettingsExtensionsTest
    {
        [Fact]
        public void GivenDefaultWhenGetKeyProviderThenReturnsNull()
        {
            // arrange
            var ps = new PublisherSettings();

            // act
            var keyProvider = ps.GetKeyProvider();

            // assert
            keyProvider.Should().BeNull();
        }

        [Fact]
        public void GivenDefaultWhenGetPartitionProviderThenReturnsNull()
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
