namespace SlimMessageBus.Host;

internal class LambdaMessageBusConfigurator : IMessageBusConfigurator
{
    private readonly string _busName;
    private readonly Action<MessageBusBuilder> _configure;

    public LambdaMessageBusConfigurator(string busName, Action<MessageBusBuilder> configure)
    {
        _busName = busName;
        _configure = configure ?? throw new ArgumentNullException(nameof(configure));
    }

    public void Configure(MessageBusBuilder builder, string busName)
    {
        if (_busName == busName)
        {
            _configure(builder);
        }
    }
}
