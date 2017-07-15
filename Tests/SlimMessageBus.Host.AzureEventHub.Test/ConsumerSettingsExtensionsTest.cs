using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub.Test
{
    [TestClass]
    public class ConsumerSettingsExtensionsTest
    {
        [TestMethod]
        public void SetsCheckpointsProperly()
        {
            // arrange
            var cs = new ConsumerSettings();

            // act
            cs.CheckpointEvery(10);
            cs.CheckpointAfter(TimeSpan.FromHours(60));

            // assert
            cs.Properties[Consts.CheckpointCount].ShouldBeEquivalentTo(10);
            cs.Properties[Consts.CheckpointDuration].ShouldBeEquivalentTo(TimeSpan.FromHours(60).TotalMilliseconds);
        }
    }
}
