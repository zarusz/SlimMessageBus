namespace SlimMessageBus;

/// <summary>
/// Lookup helper of the <see cref="IMessageBus"/> for the current execution context (singleton, thread bound (ThreadLocal) or task bound (AsyncLocal)).
/// </summary>
public static class MessageBus
{
    private static readonly Func<IMessageBus> ProviderDefault = () => throw new MessageBusException("The provider was not set");

    private static Func<IMessageBus> _provider = ProviderDefault;

    public static bool IsProviderSet() => _provider != ProviderDefault;

    public static void SetProvider(Func<IMessageBus> provider) => _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Retrieves the <see cref="IMessageBus"/> for the current execution context.        
    /// </summary>
    public static IMessageBus Current => _provider();
}