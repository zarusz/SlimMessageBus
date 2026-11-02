namespace SlimMessageBus.Host.Sql.Test;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

public class RelationalConsumerPollingTests
{
    private readonly Mock<ISqlRepository> _repository = new();
    private readonly Mock<IMessageProcessor<SqlTransportMessage>> _processor = new();
    private readonly SqlMessageBusSettings _settings = new() { PollDelay = TimeSpan.FromMinutes(1) };

    private SqlConsumer CreateConsumer(IServiceProvider services) => new(
        NullLogger<SqlConsumer>.Instance, [], [], services, _settings, _processor.Object,
        "queue", PathKind.Queue, null, "instance");

    private void SetupPoll(Func<CancellationToken, Task<IReadOnlyCollection<SqlTransportMessage>>> poll)
        => _repository.Setup(x => x.LockAndSelect("queue", PathKind.Queue, null, "instance",
            _settings.PollBatchSize, _settings.LockDuration, It.IsAny<CancellationToken>()))
            .Returns((string path, PathKind kind, string subscription, string instance, int batchSize, TimeSpan lockDuration, CancellationToken token) => poll(token));

    [Fact]
    public async Task Start_WhenEmpty_DelaysAfterFirstPollAndStopsPromptly()
    {
        var polls = 0;
        SetupPoll(_ =>
        {
            polls++;
            return Task.FromResult<IReadOnlyCollection<SqlTransportMessage>>([]);
        });
        using var services = new ServiceCollection().AddSingleton(_repository.Object).BuildServiceProvider();
        await using var consumer = CreateConsumer(services);

        await consumer.Start();

        polls.Should().Be(1);
        await consumer.Stop().WaitAsync(TimeSpan.FromSeconds(5));
        polls.Should().Be(1);
    }

    [Fact]
    public async Task Start_WhenMessagesAvailable_DrainsBatchesThenDelaysOnFirstEmptyPoll()
    {
        var message = new SqlTransportMessage { Id = Guid.NewGuid(), Headers = [] };
        var polls = 0;
        SetupPoll(_ => Task.FromResult<IReadOnlyCollection<SqlTransportMessage>>(
            ++polls <= 2 ? [message] : []));
        _processor.Setup(x => x.ProcessMessage(message, message.Headers, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ProcessMessageResult));
        _repository.Setup(x => x.Complete(It.IsAny<IReadOnlyCollection<SqlTransportMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var services = new ServiceCollection().AddSingleton(_repository.Object).BuildServiceProvider();
        await using var consumer = CreateConsumer(services);

        await consumer.Start();

        polls.Should().Be(3);
        _repository.Verify(x => x.Complete(It.IsAny<IReadOnlyCollection<SqlTransportMessage>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        await consumer.Stop().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Start_WhenImmediatePollingEnabled_PollsAgainWithoutWaitingForPollDelay()
    {
        _settings.PollImmediateDuration = TimeSpan.FromSeconds(30);
        var secondPoll = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var polls = 0;
        SetupPoll(async token =>
        {
            if (++polls == 2)
            {
                secondPoll.SetResult(true);
                // Hold the second poll until shutdown to avoid a tight loop in the test.
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return [];
        });
        using var services = new ServiceCollection().AddSingleton(_repository.Object).BuildServiceProvider();
        await using var consumer = CreateConsumer(services);

        await consumer.Start();
        await secondPoll.Task.WaitAsync(TimeSpan.FromSeconds(5));

        polls.Should().Be(2);
        await consumer.Stop().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
