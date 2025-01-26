namespace SlimMessageBus.Host;

public class ProviderExtensionProperty<T>(string key)
{
    public string Key { get; } = key;

    public void Set(HasProviderExtensions settings, T value)
        => settings.Properties[Key] = value;
}