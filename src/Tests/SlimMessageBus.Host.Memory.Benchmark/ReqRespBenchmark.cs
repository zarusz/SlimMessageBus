namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Attributes;
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

[MemoryDiagnoser]
public class ReqRespBenchmark : ReqRespBaseBenchmark
{
    [Benchmark]
    [Arguments(1000000)]
    public Task RequestResponse(int messageCount) => RunTest(messageCount);
}

[MemoryDiagnoser]
public class ReqRespWithProducerInterceptorBenchmark : ReqRespBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<IProducerInterceptor<SomeRequest>, SomeRequestProducerInterceptor>();
    }

    [Benchmark]
    [Arguments(1000000)]
    public Task ReqRespWithProducerInterceptor(int messageCount) => RunTest(messageCount);
}

[MemoryDiagnoser]
public class ReqRespWithSendInterceptorBenchmark : ReqRespBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<ISendInterceptor<SomeRequest, SomeResponse>, SomeRequestSendInterceptor>();
    }

    [Benchmark]
    [Arguments(1000000)]
    public Task ReqRespWithSendInterceptor(int messageCount) => RunTest(messageCount);
}

[MemoryDiagnoser]
public class ReqRespWithConsumerInterceptorBenchmark : ReqRespBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<IConsumerInterceptor<SomeRequest>, SomeRequestConsumerInterceptor>();
    }

    [Benchmark]
    [Arguments(1000000)]
    public Task ReqRespWithConsumerInterceptor(int messageCount) => RunTest(messageCount);
}

[MemoryDiagnoser]
public class ReqRespWithRequestHandlerInterceptorBenchmark : ReqRespBaseBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        base.Setup(services);

        services.AddTransient<IRequestHandlerInterceptor<SomeRequest, SomeResponse>, SomeRequestHandlerInterceptor>();
    }

    [Benchmark]
    [Arguments(1000000)]
    public Task ReqRespWithRequestHandlerInterceptor(int messageCount) => RunTest(messageCount);
}

public record SomeRequest(DateTimeOffset Timestamp, long Id) : IRequest<SomeResponse>;

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

public record SomeRequestSendInterceptor : ISendInterceptor<SomeRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeRequest message, Func<Task<SomeResponse>> next, IProducerContext context)
    {
        // We return immediately as we want to calculate the interceptor pipeline overhead
        return next();
    }
}

public record SomeRequestConsumerInterceptor : IConsumerInterceptor<SomeRequest>
{
    public Task<object> OnHandle(SomeRequest message, Func<Task<object>> next, IConsumerContext context)
    {
        // We return immediately as we want to calculate the interceptor pipeline overhead
        return next();
    }
}

public record SomeRequestHandlerInterceptor : IRequestHandlerInterceptor<SomeRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeRequest message, Func<Task<SomeResponse>> next, IConsumerContext context)
    {
        // We return immediately as we want to calculate the interceptor pipeline overhead
        return next();
    }
}