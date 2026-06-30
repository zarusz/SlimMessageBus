namespace SlimMessageBus.Host.Relational.Benchmark;

using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SlimMessageBus.Host;

public abstract class AbstractRelationalTransportBenchmark
{
    private ServiceProvider _serviceProvider;

    protected string QueueName { get; private set; }

    protected string SchemaName { get; private set; }

    protected IServiceProvider ServiceProvider => _serviceProvider;

    protected IMessageBus Bus => _serviceProvider.GetRequiredService<IMessageBus>();

    [Params(100, 1000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
        => StartInfrastructure().GetAwaiter().GetResult();

    [IterationSetup]
    public void IterationSetup()
        => SetupIteration().GetAwaiter().GetResult();

    [IterationCleanup]
    public void IterationCleanup()
        => CleanupIteration().GetAwaiter().GetResult();

    [GlobalCleanup]
    public void GlobalCleanup()
        => StopInfrastructure().GetAwaiter().GetResult();

    [Benchmark]
    public async Task QueuePublishConsume()
    {
        var publishTasks = Enumerable
            .Range(0, MessageCount)
            .Select(x => Bus.Publish(new BenchmarkMessage(x, DateTimeOffset.UtcNow)));

        await Task.WhenAll(publishTasks).ConfigureAwait(false);
        await WaitForMessages().ConfigureAwait(false);
    }

    protected abstract Task StartInfrastructure();

    protected abstract Task StopInfrastructure();

    protected abstract Task CreateSchema(string schemaName);

    protected abstract Task DropSchema(string schemaName);

    protected abstract void ConfigureProvider(MessageBusBuilder mbb, string schemaName);

    private async Task SetupIteration()
    {
        QueueName = $"bench_queue_{Guid.NewGuid():N}";
        SchemaName = $"smb_{Guid.NewGuid():N}";

        await CreateSchema(SchemaName).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<TestResult>();
        services.AddTransient<BenchmarkMessageConsumer>();

        services.AddSlimMessageBus(mbb =>
        {
            ConfigureProvider(mbb, SchemaName);

            mbb.Produce<BenchmarkMessage>(x =>
            {
                x.DefaultPath(QueueName);
                x.Settings.PathKind = PathKind.Queue;
            });

            mbb.Consume<BenchmarkMessage>(x =>
            {
                x.Path(QueueName);
                x.ConsumerSettings.PathKind = PathKind.Queue;
                x.WithConsumer<BenchmarkMessageConsumer>();
            });

            mbb.AddJsonSerializer();
        });

        _serviceProvider = services.BuildServiceProvider();
        await _serviceProvider.GetRequiredService<IConsumerControl>().Start().ConfigureAwait(false);
    }

    private async Task CleanupIteration()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.GetRequiredService<IConsumerControl>().Stop().ConfigureAwait(false);
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
            _serviceProvider = null;
        }

        if (SchemaName is not null)
        {
            await DropSchema(SchemaName).ConfigureAwait(false);
            SchemaName = null;
        }
    }

    private async Task WaitForMessages()
    {
        var testResult = _serviceProvider.GetRequiredService<TestResult>();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));

        while (testResult.ArrivedCount < MessageCount)
        {
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Task.Delay(1, cancellationTokenSource.Token).ConfigureAwait(false);
        }
    }
}
