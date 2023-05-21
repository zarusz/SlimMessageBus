namespace SlimMessageBus.Host.Mqtt;

static internal class HasProviderExtensionsExtensions
{
    static internal HasProviderExtensions SetMessageModifier(this HasProviderExtensions producerSettings, Action<object, MqttApplicationMessage> messageModifierAction)
    {
        producerSettings.Properties[nameof(SetMessageModifier)] = messageModifierAction;
        return producerSettings;
    }

    static internal Action<object, MqttApplicationMessage> GetMessageModifier(this HasProviderExtensions producerSettings)
    {
        return producerSettings.GetOrDefault<Action<object, MqttApplicationMessage>>(nameof(SetMessageModifier), null);
    }
}
