namespace SlimMessageBus.Host.Test.Common.IntegrationTest;

using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host;

using Xunit;

/// <summary>
/// Base integration test setup that:
/// - uses MS Dependency Injection
/// - loads app settings
/// - resolved secret values
/// - sets up a message bus
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class BaseIntegrationTest<T> : IAsyncLifetime
{
    private Lazy<ServiceProvider> _serviceProvider;
    private Action<MessageBusBuilder> messageBusBuilderAction = (mbb) => { };

    protected ILoggerFactory LoggerFactory { get; }
    protected ILogger<T> Logger { get; }
    protected IConfigurationRoot Configuration { get; }
    protected ServiceProvider ServiceProvider => _serviceProvider.Value;

    protected BaseIntegrationTest(ITestOutputHelper testOutputHelper)
    {
        LoggerFactory = new XunitLoggerFactory(testOutputHelper);
        Logger = LoggerFactory.CreateLogger<T>();

        Configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        Secrets.Load(@"..\..\..\..\..\secrets.txt");

        _serviceProvider = new Lazy<ServiceProvider>(() =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Configuration);

            services.AddSingleton(LoggerFactory);
            services.Add(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(XunitLogger<>)));
            services.Add(ServiceDescriptor.Singleton(typeof(ILogger), typeof(XunitLogger)));

            services.AddSingleton<TestMetric>();

            SetupServices(services, Configuration);

            return services.BuildServiceProvider();
        });
    }

    protected abstract void SetupServices(ServiceCollection services, IConfigurationRoot configuration);

    protected void AddBusConfiguration(Action<MessageBusBuilder> action)
    {
        var prevAction = messageBusBuilderAction;
        messageBusBuilderAction = mbb =>
        {
            prevAction(mbb);
            action(mbb);
        };
    }

    protected void ApplyBusConfiguration(MessageBusBuilder mbb) => messageBusBuilderAction?.Invoke(mbb);

    protected async Task EnsureConsumersStarted()
    {
        var timeout = Stopwatch.StartNew();
        var consumerControl = ServiceProvider.GetRequiredService<IConsumerControl>();

        // ensure the consumers are warm
        while (!consumerControl.IsStarted && timeout.ElapsedMilliseconds < 3000) await Task.Delay(200);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_serviceProvider.IsValueCreated)
        {
            await _serviceProvider.Value.DisposeAsync().ConfigureAwait(false);
        }
    }
}
