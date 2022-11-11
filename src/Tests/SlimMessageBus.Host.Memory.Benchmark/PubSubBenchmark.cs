namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.Interceptor;

public abstract class PubSubBaseBenchmark : AbstractMemoryBenchmark
{
    private readonly TestResult testResult;

    public PubSubBaseBenchmark()
    {
        testResult = svp.GetRequiredService<TestResult>();
    }

    protected override void Setup(ServiceCollection services)
    {
        services.AddSingleton<TestResult>();
        services.AddTransient<SomeEventConsumer>();
    }

    protected async Task RunTest(int messageCount)
    {
        var publishTasks = Enumerable.Range(0, messageCount).Select(x => bus.Publish(new SomeEvent(DateTimeOffset.Now, x)));

        await Task.WhenAll(publishTasks);

        while (testResult.ArrivedCount < messageCount)
        {
            await Task.Yield();
        }
    }
}

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class PubSubBenchmark : PubSubBaseBenchmark
{
    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    [Arguments(1000000)]
    public Task PubSub(int messageCount) => RunTest(messageCount);
}

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class PubSubWithProducerInterceptorBenchmark : PubSubBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<IProducerInterceptor<SomeEvent>, SomeEventProducerInterceptor>();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    [Arguments(1000000)]
    public Task PubSubWithProducerInterceptor(int messageCount) => RunTest(messageCount);
}

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class PubSubWithConsumerInterceptorBenchmark : PubSubBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<IConsumerInterceptor<SomeEvent>, SomeEventConsumerInterceptor>();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    [Arguments(1000000)]
    public Task PubSubWithConsumerInterceptor(int messageCount) => RunTest(messageCount);
}

public record SomeEvent(DateTimeOffset Timestamp, long Id);

public record SomeEventConsumer(TestResult TestResult) : IConsumer<SomeEvent>
{
    public Task OnHandle(SomeEvent message)
    {
        TestResult.OnArrived();
        return Task.CompletedTask;
    }
}

public record SomeEventProducerInterceptor : IProducerInterceptor<SomeEvent>
{
    public Task<object> OnHandle(SomeEvent message, Func<Task<object>> next, IProducerContext context)
    {
        // We return immediately as we want to calculate the interceptor pipeline overhead
        return next();
    }
}

public record SomeEventConsumerInterceptor : IConsumerInterceptor<SomeEvent>
{
    public Task OnHandle(SomeEvent message, Func<Task> next, IConsumerContext context)
    {
        // We return immediately as we want to calculate the interceptor pipeline overhead
        return next();
    }
}