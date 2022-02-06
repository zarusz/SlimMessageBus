namespace SlimMessageBus.Host.Test.Consumer
{
    using System;
    using System.Threading;
    using FluentAssertions;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class CheckpointTriggerTest
    {
        [Fact]
        public void WhenNewInstanceThenIsNotActive()
        {
            // act
            var ct = new CheckpointTrigger(2, TimeSpan.FromSeconds(5), NullLoggerFactory.Instance);

            // assert
            ct.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public void WhenAfterDurationReachedThenShouldBecomeActive()
        {
            var ct = new CheckpointTrigger(2, TimeSpan.FromSeconds(2), NullLoggerFactory.Instance);

            // act
            Thread.Sleep(2500);
            var incrementResult1 = ct.Increment();
            var incrementResult2 = ct.Increment();

            // assert
            ct.IsEnabled.Should().BeTrue();
            incrementResult1.Should().BeFalse();
            incrementResult2.Should().BeTrue();
        }

        [Fact]
        public void WhenAfterCountReachedThenShouldBecomeActive()
        {
            var ct = new CheckpointTrigger(2, TimeSpan.FromHours(1), NullLoggerFactory.Instance);

            // act
            var i1 = ct.Increment();
            var i2 = ct.Increment();

            // assert
            ct.IsEnabled.Should().BeTrue();
            i1.Should().BeFalse();
            i2.Should().BeTrue();
        }

        [Fact]
        public void WhenAfterResetThenShouldBecomeNotActive()
        {
            var ct = new CheckpointTrigger(2, TimeSpan.FromHours(1), NullLoggerFactory.Instance);
            ct.Increment();
            ct.Increment();

            // act
            ct.Reset();

            // assert
            ct.IsEnabled.Should().BeFalse();
        }
    }
}
