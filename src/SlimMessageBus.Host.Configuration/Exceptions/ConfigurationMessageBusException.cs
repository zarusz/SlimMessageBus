namespace SlimMessageBus.Host;

public class ConfigurationMessageBusException : MessageBusException
{
    public ConfigurationMessageBusException()
    {
    }

    public ConfigurationMessageBusException(string message) : base(message)
    {
    }

    public ConfigurationMessageBusException(MessageBusSettings messageBusSettings, string message)
        : this(!string.IsNullOrEmpty(messageBusSettings?.Name) ? $"Message bus with name {messageBusSettings.Name}: {message}" : message)
    {
    }

    public ConfigurationMessageBusException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
