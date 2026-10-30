namespace SlimMessageBus.Host.Outbox.Test.Services;

public class OutboxMigrationRecoveryTests
{
    [Fact]
    public async Task FailedMigration_NextPublishRecoversOnSameBus_WithFreshScope()
    {
        var migration = new Mock<IOutboxMigrationService>();
        var recovered = false;
        var scopes = new List<ScopeMarker>();
        migration.Setup(x => x.Migrate(It.IsAny<CancellationToken>()))
            .Returns(() => recovered ? Task.CompletedTask : Task.FromException(new InvalidOperationException("CREATE TABLE permission denied")));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ScopeMarker>();
        services.AddTransient(sp =>
        {
            scopes.Add(sp.GetRequiredService<ScopeMarker>());
            return migration.Object;
        });
        services.AddSingleton(sp => new OutboxSendingTask<OutboxMessage>(NullLoggerFactory.Instance, new OutboxSettings(), sp));
        services.AddSingleton<IMessageBusLifecycleInterceptor>(sp => sp.GetRequiredService<OutboxSendingTask<OutboxMessage>>());
        RecoveryTestBus transport = null;
        services.AddSlimMessageBus(mbb => mbb
            .WithProvider(settings => transport = new RecoveryTestBus(settings))
            .AutoStartConsumersEnabled(false)
            .Produce<TestMessage>(p => p.DefaultTopic("test")));
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();

        var error = await Xunit.Assert.ThrowsAsync<MessageBusException>(() => bus.Publish(new TestMessage()));
        error.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("CREATE TABLE permission denied");
        var attemptsBeforeRecovery = scopes.Count;
        transport.ProducedCount.Should().Be(0);
        recovered = true;
        await bus.Publish(new TestMessage());
        await bus.Publish(new TestMessage());

        transport.ProducedCount.Should().Be(2);
        scopes.Should().HaveCount(attemptsBeforeRecovery + 1);
        scopes.Distinct().Should().HaveCount(scopes.Count);
        scopes.Should().OnlyContain(x => x.Disposed);
    }

    private sealed record TestMessage;

    private sealed class RecoveryTestBus : MessageBusBase
    {
        public int ProducedCount { get; private set; }

        public RecoveryTestBus(MessageBusSettings settings) : base(settings) => OnBuildProvider();

        public override Task ProduceToTransport(object message, Type messageType, string path,
            IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
        {
            ProducedCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ConcurrentLifecycleCalls_ShareMigration_AndCacheSuccess()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var migration = new Mock<IOutboxMigrationService>();
        migration.Setup(x => x.Migrate(It.IsAny<CancellationToken>())).Returns(completion.Task);
        var services = new ServiceCollection();
        services.AddSingleton(migration.Object);
        await using var provider = services.BuildServiceProvider();
        await using var sendingTask = new OutboxSendingTask<OutboxMessage>(NullLoggerFactory.Instance, new OutboxSettings(), provider);

        var first = sendingTask.OnBusLifecycle(MessageBusLifecycleEventType.Created, null);
        var second = sendingTask.OnBusLifecycle(MessageBusLifecycleEventType.Created, null);
        first.Should().BeSameAs(second);
        completion.SetResult();
        await Task.WhenAll(first, second);
        await sendingTask.OnBusLifecycle(MessageBusLifecycleEventType.Created, null);

        migration.Verify(x => x.Migrate(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class ScopeMarker : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
