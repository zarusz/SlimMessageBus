namespace SlimMessageBus.Host.Outbox;

public static class SettingsExtensions
{
    public static bool IsEnabledForMessageType(this HasProviderExtensions hasProviderExtensions, MessageBusSettings messageBusSettings, string propertyEnabled, string propertyMessageTypeFilter, Type messageType)
    {
        var enabled = hasProviderExtensions.GetOrDefault(propertyEnabled, messageBusSettings, false);
        if (!enabled)
        {
            return false;
        }

        var messageTypeFilter = hasProviderExtensions.GetOrDefault<Func<Type, bool>>(propertyMessageTypeFilter, messageBusSettings, null);
        if (messageTypeFilter != null)
        {
            return messageTypeFilter(messageType);
        }

        return true;
    }
}
