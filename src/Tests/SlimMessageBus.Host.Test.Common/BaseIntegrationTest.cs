namespace SlimMessageBus.Host.Test.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SlimMessageBus.Host.Config;

/// <summary>
/// Base integration test setup that:
/// - uses MS Dependency Injection
/// - loads app settings
/// - resolved secret values
/// - sets up a message bus
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class BaseIntegrationTest<T> : IDisposable
{
    private bool _disposedValue;
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

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration);

        services.AddSingleton(LoggerFactory);
        services.Add(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(XunitLogger<>)));

        SetupServices(services, Configuration);

        _serviceProvider = new Lazy<ServiceProvider>(services.BuildServiceProvider);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (_serviceProvider != null)
                {
                    _serviceProvider.Value.DisposeAsync().GetAwaiter().GetResult();
                }
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
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
}
