using FluentAssertions;
using SlimMessageBus.Host.Config;
using System;
using Xunit;

namespace SlimMessageBus.Host.Test
{
    public class CheckpointSettingsExtensionsTest
    {
        [Fact]
        public void GivenConsumerSettingsWhenConfiguredThenCheckpointsSetProperly()
        {
            // arrange
            var cs = new ConsumerSettings();

            // act
            cs.CheckpointEvery(10);
            cs.CheckpointAfter(TimeSpan.FromHours(60));

            // assert
            cs.Properties[CheckpointSettings.CheckpointCount].Should().BeEquivalentTo(10);
            cs.Properties[CheckpointSettings.CheckpointDuration].Should().BeEquivalentTo(TimeSpan.FromHours(60));
        }

        [Fact]
        public void GivenRequestResponseSettingsWhenConfiguredThenCheckpointsSetProperly()
        {
            // arrange
            var cs = new RequestResponseSettings();

            // act
            cs.CheckpointEvery(10);
            cs.CheckpointAfter(TimeSpan.FromHours(60));

            // assert
            cs.Properties[CheckpointSettings.CheckpointCount].Should().BeEquivalentTo(10);
            cs.Properties[CheckpointSettings.CheckpointDuration].Should().BeEquivalentTo(TimeSpan.FromHours(60));
        }
    }
}
