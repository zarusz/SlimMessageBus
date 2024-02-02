namespace SlimMessageBus.Host.Test.Consumer;

public class CheckpointTriggerTest
{
    [Fact]
    public void GetValueFromSettings_Then_ValuePropertySet()
    {
        // act        
        var count = 3;
        var duration = TimeSpan.FromSeconds(2);

        var consumerSettings = new ConsumerSettings();
        consumerSettings.Properties[CheckpointSettings.CheckpointCount] = count;
        consumerSettings.Properties[CheckpointSettings.CheckpointDuration] = duration;

        var ct = CheckpointTrigger.GetCheckpointValue(consumerSettings);

        // assert
        ct.CheckpointCount.Should().Be(count);
        ct.CheckpointDuration.Should().Be(duration);
    }

    [Fact]
    public void WhenNewInstanceThenIsNotActive()
    {
        // act        
        var ct = new CheckpointTrigger(new CheckpointValue(2, TimeSpan.FromSeconds(5)), NullLoggerFactory.Instance);

        // assert
        ct.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task WhenAfterDurationReachedThenShouldBecomeActive()
    {
        var ct = new CheckpointTrigger(new CheckpointValue(2, TimeSpan.FromSeconds(2)), NullLoggerFactory.Instance);

        // act
        await Task.Delay(2500);
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
        var ct = new CheckpointTrigger(new CheckpointValue(2, TimeSpan.FromHours(1)), NullLoggerFactory.Instance);

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
        var ct = new CheckpointTrigger(new CheckpointValue(2, TimeSpan.FromHours(1)), NullLoggerFactory.Instance);
        ct.Increment();
        ct.Increment();

        // act
        ct.Reset();

        // assert
        ct.IsEnabled.Should().BeFalse();
    }
}
