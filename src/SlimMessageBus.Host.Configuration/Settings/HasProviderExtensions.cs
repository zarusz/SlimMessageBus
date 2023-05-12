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

    public T GetOrCreate<T>(string key, Func<T> factoryMethod)
    {
        if (Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        typedValue = factoryMethod();
        Properties[key] = typedValue;
        return typedValue;
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

    public T GetOrDefault<T>(string key, HasProviderExtensions parentSettings, T defaultValue = default)
    {
        if (Properties.TryGetValue(key, out var value)
            || (parentSettings != null && parentSettings.Properties.TryGetValue(key, out value)))
        {
            return (T)value;
        }
        return defaultValue;
    }
}
