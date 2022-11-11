namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.Interceptor;

public abstract class ReqRespBaseBenchmark : AbstractMemoryBenchmark
{
    private readonly TestResult testResult;

    protected ReqRespBaseBenchmark()
    {
        testResult = svp.GetRequiredService<TestResult>();
    }

    protected override void Setup(ServiceCollection services)
    {
        services.AddSingleton<TestResult>();
        services.AddTransient<SomeRequestHandler>();
    }

    public async Task RunTest(int messageCount)
    {
        var sendRequests = Enumerable.Range(0, messageCount).Select(x => bus.Send(new SomeRequest(DateTimeOffset.Now, x)));

        await Task.WhenAll(sendRequests);

        while (testResult.ArrivedCount < messageCount)
        {
            await Task.Yield();
        }
    }
}

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class ReqRespBenchmark : ReqRespBaseBenchmark
{
    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    [Arguments(1000000)]
    public Task RequestResponse(int messageCount) => RunTest(messageCount);
}

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class ReqRespWithProducerInterceptorBenchmark : ReqRespBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<IProducerInterceptor<SomeRequest>, SomeRequestProducerInterceptor>();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    [Arguments(1000000)]
    public Task ReqRespWithProducerInterceptor(int messageCount) => RunTest(messageCount);
}

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class ReqRespWithConsumerInterceptorBenchmark : ReqRespBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<IConsumerInterceptor<SomeRequest>, SomeRequestConsumerInterceptor>();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    [Arguments(100000)]
    [Arguments(1000000)]
    public Task ReqRespWithProducerInterceptor(int messageCount) => RunTest(messageCount);
}


public record SomeRequest(DateTimeOffset Timestamp, long Id) : IRequestMessage<SomeResponse>;

public record SomeResponse(DateTimeOffset Timestamp, long Id);

public record SomeRequestHandler(TestResult TestResult) : IRequestHandler<SomeRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeRequest request)
    {
        TestResult.OnArrived();
        return Task.FromResult(new SomeResponse(DateTimeOffset.Now, request.Id));
    }
}

public record SomeRequestProducerInterceptor : IProducerInterceptor<SomeRequest>
{
    public Task<object> OnHandle(SomeRequest message, Func<Task<object>> next, IProducerContext context)
    {
        // We return immediately as we want to calculate the interceptor pipeline overhead
        return next();
    }
}

public record SomeRequestConsumerInterceptor : IConsumerInterceptor<SomeRequest>
{
    public Task OnHandle(SomeRequest message, Func<Task> next, IConsumerContext context)
    {
        // We return immediately as we want to calculate the interceptor pipeline overhead
        return next();
    }
}