namespace SlimMessageBus.Host;

public static class MessageScope
{
    private static readonly AsyncLocal<IDependencyResolver> _current = new();

    public static IDependencyResolver Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
