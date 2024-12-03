namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Interceptor;

public abstract class ReqRespBaseBenchmark : AbstractMemoryBenchmark
{
    protected override void Setup(ServiceCollection services)
    {
        services.AddSingleton<TestResult>();
        services.AddTransient<SomeRequestHandler>();
    }

    public async Task RunTest(int messageCount, bool createMessageScope)
    {
        PerMessageScopeEnabled = createMessageScope;
        var bus = Bus;
        var sendRequests = Enumerable.Range(0, messageCount).Select(x => bus.Send(new SomeRequest(DateTimeOffset.Now, x)));

        await Task.WhenAll(sendRequests);

        var testResult = ServiceProvider.GetRequiredService<TestResult>();
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
    [Arguments(1000000, true)]
    [Arguments(1000000, false)]
    public Task RequestResponse(int messageCount, bool createMessageScope) => RunTest(messageCount, createMessageScope);
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
    [Arguments(1000000, true)]
    [Arguments(1000000, false)]
    public Task ReqRespWithProducerInterceptor(int messageCount, bool createMessageScope) => RunTest(messageCount, createMessageScope);
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
    [Arguments(1000000, true)]
    [Arguments(1000000, false)]
    public Task ReqRespWithSendInterceptor(int messageCount, bool createMessageScope) => RunTest(messageCount, createMessageScope);
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
    [Arguments(1000000, true)]
    [Arguments(1000000, false)]
    public Task ReqRespWithConsumerInterceptor(int messageCount, bool createMessageScope) => RunTest(messageCount, createMessageScope);
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
    [Arguments(1000000, true)]
    [Arguments(1000000, false)]
    public Task ReqRespWithRequestHandlerInterceptor(int messageCount, bool createMessageScope) => RunTest(messageCount, createMessageScope);
}

public record SomeRequest(DateTimeOffset Timestamp, long Id) : IRequest<SomeResponse>;

public record SomeResponse(DateTimeOffset Timestamp, long Id);

public record SomeRequestHandler(TestResult TestResult) : IRequestHandler<SomeRequest, SomeResponse>
{
    public Task<SomeResponse> OnHandle(SomeRequest request, CancellationToken cancellationToken)
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