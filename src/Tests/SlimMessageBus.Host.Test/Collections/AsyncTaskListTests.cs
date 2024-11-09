namespace SlimMessageBus.Host.Test.Collections;

using System.Collections.Concurrent;

using SlimMessageBus.Host.Collections;

public class AsyncTaskListTests
{
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
