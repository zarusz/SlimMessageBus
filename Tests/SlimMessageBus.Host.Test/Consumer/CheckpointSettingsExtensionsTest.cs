using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Test
{
    [TestClass]
    public class CheckpointSettingsExtensionsTest
    {
        [TestMethod]
        public void ConsumerSettings_SetsCheckpointsProperly()
        {
            // arrange
            var cs = new ConsumerSettings();

            // act
            cs.CheckpointEvery(10);
            cs.CheckpointAfter(TimeSpan.FromHours(60));

            // assert
            cs.Properties[CheckpointSettings.CheckpointCount].ShouldBeEquivalentTo(10);
            cs.Properties[CheckpointSettings.CheckpointDuration].ShouldBeEquivalentTo(TimeSpan.FromHours(60));
        }

        [TestMethod]
        public void RequestResponseSettings_SetsCheckpointsProperly()
        {
            // arrange
            var cs = new RequestResponseSettings();

            // act
            cs.CheckpointEvery(10);
            cs.CheckpointAfter(TimeSpan.FromHours(60));

            // assert
            cs.Properties[CheckpointSettings.CheckpointCount].ShouldBeEquivalentTo(10);
            cs.Properties[CheckpointSettings.CheckpointDuration].ShouldBeEquivalentTo(TimeSpan.FromHours(60));
        }
    }
}
