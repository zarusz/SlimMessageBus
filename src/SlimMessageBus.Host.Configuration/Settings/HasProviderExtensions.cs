namespace SlimMessageBus.Host;

public abstract class HasProviderExtensions
{
    /// <summary>
    /// Provider specific properties bag.
    /// </summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

    public T GetOrDefault<T>(string key, T defaultValue = default)
    {
        if (Properties.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return defaultValue;
    }

    public T GetOrDefault<T>(string key, MessageBusSettings messageBusSettings, T defaultValue = default)
    {
        if (Properties.TryGetValue(key, out var value)
            || messageBusSettings.Properties.TryGetValue(key, out value)
            || (messageBusSettings.Parent != null && messageBusSettings.Parent.Properties.TryGetValue(key, out value)))
        {
            return (T)value;
        }
        return defaultValue;
    }
}
