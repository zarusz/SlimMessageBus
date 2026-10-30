namespace SlimMessageBus.Host.Test.Collections;

using System.Collections.Concurrent;

using SlimMessageBus.Host.Collections;

public class AsyncTaskListTests
{
    [Fact]
    public async Task FailedInitialization_RetriesWithoutRepeatingSuccessfulPredecessors()
    {
        var subject = new AsyncTaskList();
        var successfulCalls = 0;
        var failingCalls = 0;
        var followingCalls = 0;
        var recovered = false;
        subject.Add(() => { successfulCalls++; return Task.CompletedTask; }, default);
        subject.Add(() =>
        {
            failingCalls++;
            return recovered ? Task.CompletedTask : Task.FromException(new InvalidOperationException("migration failed"));
        }, default);
        subject.Add(() => { followingCalls++; return Task.CompletedTask; }, default);

        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(subject.EnsureAllFinished);
        var attemptsBeforeRecovery = failingCalls;
        recovered = true;
        await subject.EnsureAllFinished();
        await subject.EnsureAllFinished();

        successfulCalls.Should().Be(1);
        failingCalls.Should().Be(attemptsBeforeRecovery + 1);
        followingCalls.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentWaiters_ShareRetryAttempt()
    {
        var subject = new AsyncTaskList();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var recovered = false;
        subject.Add(() =>
        {
            calls++;
            return recovered ? completion.Task : Task.FromException(new InvalidOperationException());
        }, default);
        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(subject.EnsureAllFinished);
        var attemptsBeforeRecovery = calls;
        recovered = true;

        var first = subject.EnsureAllFinished();
        var second = subject.EnsureAllFinished();
        calls.Should().Be(attemptsBeforeRecovery + 1);
        completion.SetResult();
        await Task.WhenAll(first, second);
        await subject.EnsureAllFinished();
        calls.Should().Be(attemptsBeforeRecovery + 1);
    }

    [Fact]
    public async Task CanceledInitialization_CanRecoverOnNextAttempt()
    {
        var subject = new AsyncTaskList();
        var recovered = false;
        subject.Add(() => recovered ? Task.CompletedTask : Task.FromCanceled(new CancellationToken(true)), default);
        await Xunit.Assert.ThrowsAnyAsync<OperationCanceledException>(subject.EnsureAllFinished);
        recovered = true;
        await subject.EnsureAllFinished();
    }

    [Fact]
    public async Task Given_TaskAdded_When_EnsureAllFinished_Then_TaskIsCompleted()
    {
        // arrange

        var numberList = new ConcurrentQueue<int>();

        async Task RunTask(int n)
        {
            await Task.Delay(100);
            numberList.Enqueue(n);
        }

        var subject = new AsyncTaskList();
        subject.Add(() => RunTask(1), default);
        subject.Add(() => RunTask(2), default);

        // act
        await subject.EnsureAllFinished();

        // assert
        numberList.Should().HaveCount(2);
        numberList.TryDequeue(out var n1).Should().BeTrue();
        n1.Should().Be(1);
        numberList.TryDequeue(out var n2).Should().BeTrue();
        n2.Should().Be(2);
    }
}
