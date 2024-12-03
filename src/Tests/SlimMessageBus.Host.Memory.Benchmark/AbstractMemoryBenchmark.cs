namespace SlimMessageBus.Host.Memory.Benchmark;

using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SlimMessageBus.Host;

public abstract class AbstractMemoryBenchmark : IDisposable
{
    private Lazy<ServiceProvider> _serviceProvider;

    protected IServiceProvider ServiceProvider => _serviceProvider.Value;

    protected bool PerMessageScopeEnabled { get; set; }

    protected IMessageBus Bus => ServiceProvider.GetRequiredService<IMessageBus>();

    protected AbstractMemoryBenchmark()
    {
        _serviceProvider = new Lazy<ServiceProvider>(() =>
        {
            var services = new ServiceCollection();

            services.AddSlimMessageBus(mbb => mbb
                .WithProviderMemory()
                .AutoDeclareFrom(Assembly.GetExecutingAssembly())
                .PerMessageScopeEnabled(PerMessageScopeEnabled));

            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddSingleton<TestResult>();
            services.AddTransient<SomeRequestHandler>();
            Setup(services);

            return services.BuildServiceProvider();
        });
    }

    protected virtual void Setup(ServiceCollection services)
    {
    }

    public void Dispose()
    {
        if (_serviceProvider.Value != null)
        {
            _serviceProvider.Value.Dispose();
            _serviceProvider = null;
        }
    }
}
