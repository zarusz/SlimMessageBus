namespace SlimMessageBus.Host.Test.Consumer;

public class ConcurrentMessageProcessorDecoratorTest
{
    private readonly MessageBusMock _busMock;
    private readonly Mock<IMessageProcessor<SomeMessage>> _messageProcessorMock;

    public ConcurrentMessageProcessorDecoratorTest()
    {
        _busMock = new MessageBusMock();
        _messageProcessorMock = new Mock<IMessageProcessor<SomeMessage>>();
    }

    [Fact]
    public void When_Dispose_Then_CallsDisposeOnTarget()
    {
        // arrange
        var targetDisposableMock = _messageProcessorMock.As<IDisposable>();
        var subject = new ConcurrentMessageProcessorDecorator<SomeMessage>(1, NullLoggerFactory.Instance, _messageProcessorMock.Object);

        // act
        subject.Dispose();

        // assert
        targetDisposableMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_WaitAll_Then_WaitsOnAllPendingMessageProcessToFinish(bool cancelAwait)
    {
        // arrange
        _messageProcessorMock
            .Setup(x => x.ProcessMessage(
                It.IsAny<SomeMessage>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                return new(ProcessResult.Success, null, null, null);
            });

        var subject = new ConcurrentMessageProcessorDecorator<SomeMessage>(1, NullLoggerFactory.Instance, _messageProcessorMock.Object);

        await subject.ProcessMessage(new SomeMessage(), new Dictionary<string, object>(), default);

        using var cts = new CancellationTokenSource();

        if (cancelAwait)
        {
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        }

        // act
        var waitAll = () => subject.WaitAll(cts.Token);

        // assert
        if (cancelAwait)
        {
            await waitAll.Should().ThrowAsync<TaskCanceledException>();
        }
        else
        {
            await waitAll.Should().NotThrowAsync();
        }
        _messageProcessorMock
            .Verify(x => x.ProcessMessage(
                It.IsAny<SomeMessage>(),
                It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IServiceProvider>(),
                It.IsAny<CancellationToken>()),
                cancelAwait ? Times.Once : Times.Once);
    }

    [Theory]
    [InlineData(10, 40)]
    [InlineData(2, 40)]
    public async Task When_ProcessMessage_Given_NMessagesAndConcurrencySetToC_Then_NMethodInvocationsHappenOnTargetWithCConcurrently(int concurrency, int expectedMessageCount)
    {
        // arrange
        var subject = new ConcurrentMessageProcessorDecorator<SomeMessage>(concurrency, NullLoggerFactory.Instance, _messageProcessorMock.Object);

        var currentSectionCount = 0;
        var maxSectionCount = 0;
        var maxSectionCountLock = new object();
        var messageCount = 0;

        _messageProcessorMock
            .Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                // Entering critical section
                Interlocked.Increment(ref currentSectionCount);

                // Simulate work
                await Task.Delay(50);

                Interlocked.Increment(ref messageCount);

                lock (maxSectionCountLock)
                {
                    if (currentSectionCount > maxSectionCount)
                    {
                        maxSectionCount = currentSectionCount;
                    }
                }

                // Simulate work
                await Task.Delay(50);

                // Leaving critical section
                Interlocked.Decrement(ref currentSectionCount);
                return new(ProcessResult.Success, null, null, null);
            });

        // act
        var msg = new SomeMessage();
        var msgHeaders = new Dictionary<string, object>();
        for (var i = 0; i < expectedMessageCount; i++)
        {
            // executed in sequence
            await subject.ProcessMessage(msg, msgHeaders, default);
        }

        // assert
        while (subject.PendingCount > 0)
        {
            await Task.Delay(100);
        }

        messageCount.Should().Be(expectedMessageCount);
        maxSectionCount.Should().Be(concurrency);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ExceptionHappensOnTarget_Then_ExceptionIsReportedOnSecondInvocation()
    {
        // arrange
        var subject = new ConcurrentMessageProcessorDecorator<SomeMessage>(1, NullLoggerFactory.Instance, _messageProcessorMock.Object);

        var exception = new Exception("Boom!");

        _messageProcessorMock
            .Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessMessageResult(ProcessResult.Failure, exception, null, null));

        var msg = new SomeMessage();
        var msgHeaders = new Dictionary<string, object>();
        await subject.ProcessMessage(msg, msgHeaders, default);

        // act
        var result = await subject.ProcessMessage(msg, msgHeaders, default);

        // assert
        result.Exception.Should().Be(exception);
    }
}
