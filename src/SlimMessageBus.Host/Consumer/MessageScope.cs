namespace SlimMessageBus.Host.Consumer;

public static class MessageScope
{
    private static readonly AsyncLocal<(IServiceProvider ServiceProvider, ConsumerContextScope ConsumerScope)> _current = new();

    public static IServiceProvider Current
    {
        get => _current.Value.ServiceProvider;
        set => _current.Value = (value, _current.Value.ConsumerScope);
    }

    internal static ConsumerContextScope CurrentConsumerScope
    {
        get => _current.Value.ConsumerScope;
        set => _current.Value = (_current.Value.ServiceProvider, value);
    }
}
