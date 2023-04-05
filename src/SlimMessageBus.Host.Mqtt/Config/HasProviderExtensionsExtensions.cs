namespace SlimMessageBus.Host.Mqtt;

internal static class HasProviderExtensionsExtensions
{
    internal static HasProviderExtensions SetMessageModifier(this HasProviderExtensions producerSettings, Action<object, MqttApplicationMessage> messageModifierAction)
    {
        producerSettings.Properties[nameof(SetMessageModifier)] = messageModifierAction;
        return producerSettings;
    }

    internal static Action<object, MqttApplicationMessage> GetMessageModifier(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<object, MqttApplicationMessage>>(nameof(SetMessageModifier), null);
    }
}
