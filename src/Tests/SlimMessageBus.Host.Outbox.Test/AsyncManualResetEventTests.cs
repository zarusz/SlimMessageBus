namespace SlimMessageBus.Host.Outbox.Test;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "Testing async state")]
public class AsyncManualResetEventTests
{
    [Fact]
    public void Constructor_InitialStateFalse_ShouldNotBeSet()
    {
        // arrange
        var asyncEvent = new AsyncManualResetEvent(false);

        // act
        var result = asyncEvent.Wait(100).Result;

        // assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitialStateTrue_ShouldBeSet()
    {
        // arrange
        var asyncEvent = new AsyncManualResetEvent(true);

        // act
        var result = asyncEvent.Wait(100).Result;

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Set_ShouldSetEvent()
    {
        // arrange
        var asyncEvent = new AsyncManualResetEvent(false);

        // act
        asyncEvent.Set();

        // assert
        asyncEvent.Wait(0).Result.Should().BeTrue();
    }

    [Fact]
    public void Reset_ShouldResetEvent()
    {
        // arrange
        var asyncEvent = new AsyncManualResetEvent(true);

        // act
        asyncEvent.Reset();

        // assert
        asyncEvent.Wait(0).Result.Should().BeFalse();
    }

    [Fact]
    public async Task Wait_ShouldBlockUntilSet()
    {
        // arrange
        var asyncEvent = new AsyncManualResetEvent(false);

        var waitTask = Task.Run(async () =>
        {
            return await asyncEvent.Wait();
        });

        await Task.Delay(100);
        waitTask.IsCompleted.Should().BeFalse();

        // act
        asyncEvent.Set();

        // assert

        await Task.WhenAny(waitTask, Task.Delay(500));
        waitTask.IsCompleted.Should().BeTrue();
        (await waitTask).Should().BeTrue();
    }

    [Fact]
    public async Task Wait_ShouldTimeout()
    {
        // arrange
        var asyncEvent = new AsyncManualResetEvent(false);

        // act
        var result = await asyncEvent.Wait(10);
        
        // assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Wait_ShouldCancel()
    {
        // arrange
        var asyncEvent = new AsyncManualResetEvent(false);
        var cts = new CancellationTokenSource();

        var waitTask = Task.Run(async () =>
        {
            return await asyncEvent.Wait(Timeout.Infinite, cts.Token);
        });

        await Task.Delay(100);
        waitTask.IsCompleted.Should().BeFalse();

        // act
        cts.Cancel();

        // assert
        (await waitTask).Should().BeFalse();
        waitTask.IsCompleted.Should().BeTrue();
    }
}
