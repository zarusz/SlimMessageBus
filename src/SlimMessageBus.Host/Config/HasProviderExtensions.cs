namespace SlimMessageBus.Host.Config;

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
            return (T) value;
        }
        return defaultValue;
    }
}