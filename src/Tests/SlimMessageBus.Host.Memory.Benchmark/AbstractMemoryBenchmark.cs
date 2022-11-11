namespace SlimMessageBus.Host.Memory.Benchmark;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host.MsDependencyInjection;
using System.Reflection;

public abstract class AbstractMemoryBenchmark : IDisposable
{
    protected ServiceProvider svp;
    protected readonly IMessageBus bus;

    protected AbstractMemoryBenchmark()
    {
        var services = new ServiceCollection();

        services.AddSlimMessageBus(mbb => mbb.WithProviderMemory().AutoDeclareFrom(Assembly.GetExecutingAssembly()));

        services.AddSingleton<TestResult>();
        services.AddTransient<SomeRequestHandler>();
        Setup(services);

        svp = services.BuildServiceProvider();

        bus = svp.GetRequiredService<IMessageBus>();
    }

    protected virtual void Setup(ServiceCollection services)
    {
    }

    public void Dispose()
    {
        if (svp != null)
        {
            svp.Dispose();
            svp = null;
        }
    }
}
