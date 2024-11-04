namespace SlimMessageBus.Host.Test.Common.IntegrationTest;

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
    private readonly Lazy<ServiceProvider> _serviceProvider;
    private Action<MessageBusBuilder> messageBusBuilderAction = (mbb) => { };

    private ILogger<T>? _logger;
    protected ILogger<T> Logger => _logger ??= ServiceProvider.GetRequiredService<ILogger<T>>();

    protected IConfigurationRoot Configuration { get; }
    protected ServiceProvider ServiceProvider => _serviceProvider.Value;

    protected BaseIntegrationTest(ITestOutputHelper output)
    {
        // Creating a `LoggerProviderCollection` lets Serilog optionally write
        // events through other dynamically-added MEL ILoggerProviders.
        var providers = new LoggerProviderCollection();

        Configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        Log.Logger = new LoggerConfiguration()
            //.WriteTo.Providers(providers)
            .WriteTo.TestOutput(output, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .ReadFrom.Configuration(Configuration)
            .CreateLogger();

        Secrets.Load(@"..\..\..\..\..\secrets.txt");

        _serviceProvider = new Lazy<ServiceProvider>(() =>
        {
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(Configuration);
            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

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
        await consumerControl.Start();

        // ensure the consumers are warm
        while (!consumerControl.IsStarted && timeout.ElapsedMilliseconds < 5000) await Task.Delay(100);
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
