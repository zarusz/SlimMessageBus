namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.MsDependencyInjection;
using System.Reflection;

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class PubSubBenchmark : IDisposable
{
    private ServiceProvider svp;
    private TestResult testResult;
    private IMessageBus bus;

    public PubSubBenchmark()
    {
        var services = new ServiceCollection();

        services.AddSlimMessageBus((mbb, _) =>
        {
            mbb
                .WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly());
            //.Produce<SomeEvent>(x => x.DefaultPath(x.MessageType.Name))
            //.Consume<SomeEvent>(x => x.Topic(x.MessageType.Name).WithConsumer<SomeEventConsumer>());
        });

        services.AddSingleton<TestResult>();
        services.AddTransient<SomeEventConsumer>();

        svp = services.BuildServiceProvider();

        bus = svp.GetRequiredService<IMessageBus>();
        testResult = svp.GetRequiredService<TestResult>();
    }

    public void Dispose()
    {
        if (svp != null)
        {
            svp.Dispose();
            svp = null;
        }
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    [Arguments(1000000)]
    public async Task PubSub(int messageCount)
    {
        var publishTasks = Enumerable.Range(0, messageCount).Select(x => bus.Publish(new SomeEvent(DateTimeOffset.Now, x)));

        await Task.WhenAll(publishTasks);

        while (testResult.ArrivedCount < messageCount)
        {
            await Task.Yield();
        }
    }
}

public record SomeEvent(DateTimeOffset Timestamp, long Id);

public class SomeEventConsumer : IConsumer<SomeEvent>
{
    private readonly TestResult testResult;

    public SomeEventConsumer(TestResult testResult) => this.testResult = testResult;

    public Task OnHandle(SomeEvent message, string path)
    {
        testResult.OnArrived();
        return Task.CompletedTask;
    }
}
