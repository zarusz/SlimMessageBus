namespace SlimMessageBus.Host.Test.Consumer;

public class ConcurrentMessageProcessorDecoratorTest
{
    private readonly Mock<IMessageProcessor<SomeMessage>> _messageProcessorMock;

    public ConcurrentMessageProcessorDecoratorTest()
    {
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
    public async Task BackgroundFailureIsLoggedAndTheNextDeliveryIsProcessed()
    {
        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        using var subject = new ConcurrentMessageProcessorDecorator<SomeMessage>(2, loggerFactory.Object, _messageProcessorMock.Object);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var exception = new Exception("Boom!");
        var first = new SomeMessage();
        var second = new SomeMessage();

        _messageProcessorMock
            .Setup(x => x.ProcessMessage(It.Is<SomeMessage>(m => ReferenceEquals(m, first)), null, null, null, timeout.Token))
            .ReturnsAsync(new ProcessMessageResult(ProcessResult.Failure, exception, null, null));
        _messageProcessorMock
            .Setup(x => x.ProcessMessage(It.Is<SomeMessage>(m => ReferenceEquals(m, second)), null, null, null, timeout.Token))
            .ReturnsAsync(new ProcessMessageResult(ProcessResult.Success, null, null, null));

        await subject.ProcessMessage(first, null, cancellationToken: timeout.Token);
        await subject.WaitAll(timeout.Token);
        var result = await subject.ProcessMessage(second, null, cancellationToken: timeout.Token);
        await subject.WaitAll(timeout.Token);

        result.Exception.Should().BeNull();
        _messageProcessorMock.Verify(x => x.ProcessMessage(It.Is<SomeMessage>(m => ReferenceEquals(m, second)), null, null, null, timeout.Token), Times.Once);
        logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), exception, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_BackgroundExceptionReportingDisabled_Then_ExceptionIsNotReportedOnSecondInvocation()
    {
        // arrange
        var subject = new ConcurrentMessageProcessorDecorator<SomeMessage>(1, NullLoggerFactory.Instance, _messageProcessorMock.Object, reportBackgroundExceptions: false);

        var exception = new Exception("Boom!");
        var firstCallCompleted = new TaskCompletionSource<bool>();
        var callCount = 0;

        _messageProcessorMock
            .Setup(x => x.ProcessMessage(It.IsAny<SomeMessage>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    firstCallCompleted.TrySetResult(true);
                }
                return Task.FromResult(new ProcessMessageResult(ProcessResult.Failure, exception, null, null));
            });

        var msg = new SomeMessage();
        var msgHeaders = new Dictionary<string, object>();
        await subject.ProcessMessage(msg, msgHeaders, default);
        await firstCallCompleted.Task;
        await subject.WaitAll(CancellationToken.None);

        // act
        var result = await subject.ProcessMessage(msg, msgHeaders, default);
        await subject.WaitAll(CancellationToken.None);

        // assert
        result.Exception.Should().BeNull();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task When_ProcessMessage_Given_ExceptionHappensOnTarget_Then_SemaphoreIsNotLeakedAndProcessingContinues()
    {
        // arrange
        using var subject = new ConcurrentMessageProcessorDecorator<SomeMessage>(1, NullLoggerFactory.Instance, _messageProcessorMock.Object);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var exception = new Exception("Boom!");
        var message = new SomeMessage();
        _messageProcessorMock.SetupSequence(x => x.ProcessMessage(message, null, null, null, timeout.Token))
            .ThrowsAsync(exception)
            .ReturnsAsync(new ProcessMessageResult(ProcessResult.Success, null, null, null));

        await subject.ProcessMessage(message, null, cancellationToken: timeout.Token);
        await subject.ProcessMessage(message, null, cancellationToken: timeout.Token);
        await subject.WaitAll(timeout.Token);

        _messageProcessorMock.Verify(x => x.ProcessMessage(message, null, null, null, timeout.Token), Times.Exactly(2));
        subject.PendingCount.Should().Be(0);
    }
}
