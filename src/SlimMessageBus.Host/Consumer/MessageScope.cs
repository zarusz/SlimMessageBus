namespace SlimMessageBus.Host.Consumer;

public static class MessageScope
{
    private static readonly AsyncLocal<IServiceProvider> _current = new();

    public static IServiceProvider Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
