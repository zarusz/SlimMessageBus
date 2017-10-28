using System;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace SlimMessageBus.Host.Test.Consumer
{
    public class CheckpointTriggerTest
    {
        [Fact]
        public void InitialState_IsNotActive()
        {
            // act
            var ct = new CheckpointTrigger(2, TimeSpan.FromSeconds(5));

            // assert
            ct.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void AfterDurationReached_ShouldBecomeActive()
        {
            var ct = new CheckpointTrigger(2, TimeSpan.FromSeconds(2));

            // act
            Thread.Sleep(2500);
            var isActive = ct.Increment();

            // assert
            ct.IsEnabled.Should().BeTrue();
            isActive.Should().BeTrue();
        }

        [Fact]
        public void AfterCountReached_ShouldBecomeActive()
        {
            var ct = new CheckpointTrigger(2, TimeSpan.FromHours(1));

            // act
            var i1 = ct.Increment();
            var i2 = ct.Increment();

            // assert
            ct.IsEnabled.Should().BeTrue();
            i1.Should().BeFalse();
            i2.Should().BeTrue();
        }

        [Fact]
        public void AfterReset_ShouldBecomeNotActive()
        {
            var ct = new CheckpointTrigger(2, TimeSpan.FromHours(1));
            ct.Increment();
            ct.Increment();

            // act
            ct.Reset();

            // assert
            ct.IsEnabled.Should().BeFalse();
        }
    }
}
