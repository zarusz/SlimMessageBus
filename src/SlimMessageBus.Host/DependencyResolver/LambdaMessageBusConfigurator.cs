namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Config;

internal class LambdaMessageBusConfigurator : IMessageBusConfigurator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _busName;
    private readonly Action<MessageBusBuilder, IServiceProvider> _configure;

    public LambdaMessageBusConfigurator(IServiceProvider serviceProvider, string busName, Action<MessageBusBuilder, IServiceProvider> configure)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _busName = busName;
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    public void Configure(MessageBusBuilder builder, string busName)
    {
        if (_busName == busName)
        {
            _configure(builder, _serviceProvider);
        }
    }
}
