namespace SlimMessageBus.Host.Mqtt;

public static class ProducerBuilderExtensions
{
    /// <summary>
    /// Allows to set additional properties to the native message <see cref="MqttApplicationMessage"/> when producing the <see cref="T"/> message.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="producerBuilder"></param>
    /// <param name="modifierAction"></param>
    /// <returns></returns>
    public static ProducerBuilder<T> WithModifier<T>(this ProducerBuilder<T> producerBuilder, Action<T, MqttApplicationMessage> modifierAction)
    {
        if (producerBuilder is null) throw new ArgumentNullException(nameof(producerBuilder));
        if (modifierAction is null) throw new ArgumentNullException(nameof(modifierAction));

        producerBuilder.Settings.SetMessageModifier((e, m) => modifierAction((T)e, m));
        return producerBuilder;
    }
}