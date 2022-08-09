namespace SlimMessageBus.Host.Memory.Benchmark;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.MsDependencyInjection;
using System.Reflection;

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class ReqRespBenchmark : IDisposable
{
    private ServiceProvider svp;
    private readonly TestResult testResult;
    private readonly IMessageBus bus;

    public ReqRespBenchmark()
    {
        var services = new ServiceCollection();

        services.AddSlimMessageBus((mbb, _) =>
        {
            mbb
                .WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly());
            //.Produce<SomeRequest>(x => x.DefaultTopic(x.MessageType.Name))
            //.Handle<SomeRequest, SomeResponse>(x => x.Topic(x.MessageType.Name).WithHandler<SomeRequestHandler>());
        });

        services.AddSingleton<TestResult>();
        services.AddTransient<SomeRequestHandler>();

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
    public async Task RequestResponse(int messageCount)
    {
        var sendRequests = Enumerable.Range(0, messageCount).Select(x => bus.Send(new SomeRequest(DateTimeOffset.Now, x)));

        await Task.WhenAll(sendRequests);

        while (testResult.ArrivedCount < messageCount)
        {
            await Task.Yield();
        }
    }
}

public record SomeRequest(DateTimeOffset Timestamp, long Id) : IRequestMessage<SomeResponse>;

public record SomeResponse(DateTimeOffset Timestamp, long Id);

public class SomeRequestHandler : IRequestHandler<SomeRequest, SomeResponse>
{
    private readonly TestResult testResult;

    public SomeRequestHandler(TestResult testResult) => this.testResult = testResult;

    public Task<SomeResponse> OnHandle(SomeRequest request, string path)
    {
        testResult.OnArrived();
        return Task.FromResult(new SomeResponse(DateTimeOffset.Now, request.Id));
    }
}