namespace SlimMessageBus.Host.Outbox.Test.Services;

using System.Collections.Concurrent;

public class OutboxReconnectTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("poll")]
    [InlineData("resolution")]
    public async Task Polling_UsesFreshScopes_AndRecoversWithoutRestart(string firstFailure)
    {
        var scopes = new ConcurrentQueue<ScopeMarker>();
        var repositories = new ConcurrentQueue<Mock<IOutboxMessageRepository<OutboxMessage>>>();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextId = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IMessageBus>());
        services.AddSingleton(Mock.Of<IInstanceIdProvider>(x => x.GetInstanceId() == "sender"));
        services.AddScoped<IOutboxLockRenewalTimerFactory, OutboxLockRenewalTimerFactory<OutboxMessage>>();
        services.AddScoped(_ =>
        {
            var marker = new ScopeMarker(Interlocked.Increment(ref nextId));
            scopes.Enqueue(marker);
            return marker;
        });
        services.AddScoped(sp =>
        {
            var marker = sp.GetRequiredService<ScopeMarker>();
            if (marker.Id == 1 && firstFailure == "resolution")
            {
                throw new InvalidOperationException("connection factory unavailable");
            }
            var repository = new Mock<IOutboxMessageRepository<OutboxMessage>>();
            repositories.Enqueue(repository);
            repository.Setup(x => x.LockAndSelect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (marker.Id == 1 && firstFailure == "poll")
                    {
                        return Task.FromException<IReadOnlyCollection<OutboxMessage>>(new InvalidOperationException("connection dropped"));
                    }
                    if (marker.Id > 1)
                    {
                        recovered.TrySetResult();
                    }
                    return Task.FromResult<IReadOnlyCollection<OutboxMessage>>([]);
                });
            return repository.Object;
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var sender = new OutboxSendingTask<OutboxMessage>(NullLoggerFactory.Instance,
            new OutboxSettings { PollIdleSleep = TimeSpan.FromMilliseconds(20) }, provider);

        await sender.OnBusLifecycle(MessageBusLifecycleEventType.Started, null);
        try
        {
            await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await sender.OnBusLifecycle(MessageBusLifecycleEventType.Stopping, null);
        }

        scopes.Count.Should().BeGreaterThan(1);
        scopes.Should().OnlyContain(x => x.Disposed);
        foreach (var repository in repositories)
        {
            repository.Verify(x => x.LockAndSelect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.AtMostOnce);
        }
    }

    [Fact]
    public async Task Shutdown_CancelsDatabaseOperation_AndDisposesScope()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var marker = new ScopeMarker(1);
        var repository = new Mock<IOutboxMessageRepository<OutboxMessage>>();
        repository.Setup(x => x.LockAndSelect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, int _, bool _, TimeSpan _, CancellationToken token) =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
                return Array.Empty<OutboxMessage>();
            });
        var timer = Mock.Of<IOutboxLockRenewalTimer>();
        var factory = new Mock<IOutboxLockRenewalTimerFactory>();
        factory.Setup(x => x.CreateRenewalTimer(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<Action<Exception>>(), It.IsAny<CancellationToken>())).Returns(timer);
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IMessageBus>());
        services.AddSingleton(factory.Object);
        services.AddScoped(_ => marker);
        services.AddScoped(sp => { sp.GetRequiredService<ScopeMarker>(); return repository.Object; });
        await using var provider = services.BuildServiceProvider();
        await using var sender = new OutboxSendingTask<OutboxMessage>(NullLoggerFactory.Instance, new OutboxSettings(), provider);

        await sender.OnBusLifecycle(MessageBusLifecycleEventType.Started, null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sender.OnBusLifecycle(MessageBusLifecycleEventType.Stopping, null).WaitAsync(TimeSpan.FromSeconds(5));

        marker.Disposed.Should().BeTrue();
    }

    private sealed class ScopeMarker(int id) : IAsyncDisposable
    {
        public int Id { get; } = id;
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return default;
        }
    }
}
