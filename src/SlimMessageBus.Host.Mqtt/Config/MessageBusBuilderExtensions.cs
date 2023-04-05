namespace SlimMessageBus.Host.Mqtt;

public static class MessageBusBuilderExtensions
{
    public static MessageBusBuilder WithProviderMqtt(this MessageBusBuilder mbb, Action<MqttMessageBusSettings> configure)
    {
        if (mbb == null) throw new ArgumentNullException(nameof(mbb));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var providerSettings = new MqttMessageBusSettings();
        configure(providerSettings);

        return mbb.WithProvider(settings => new MqttMessageBus(settings, providerSettings));
    }

    /// <summary>
    /// Allows to set additional properties to the native <see cref="MqttApplicationMessage"/> message produced.
    /// </summary>
    /// <param name="mbb"></param>
    /// <param name="modifierAction"></param>
    /// <returns></returns>
    public static MessageBusBuilder WithModifier(this MessageBusBuilder mbb, Action<object, MqttApplicationMessage> modifierAction)
    {
        if (mbb is null) throw new ArgumentNullException(nameof(mbb));
        if (modifierAction is null) throw new ArgumentNullException(nameof(modifierAction));

        mbb.Settings.SetMessageModifier(modifierAction);
        return mbb;
    }
}
