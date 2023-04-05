namespace SlimMessageBus.Host.Mqtt;

public class MqttMessageBusSettings
{
    public MqttFactory MqttFactory { get; set; } = new();
    /// <summary>
    /// Used to build the underlying <see cref="MqttClient"/> client.
    /// </summary>
    public MqttClientOptionsBuilder ClientBuilder { get; set; } = new();
    /// <summary>
    /// Used to build the underlying <see cref="ManagedMqttClient"/> client wrapper.
    /// </summary>
    public ManagedMqttClientOptionsBuilder ManagedClientBuilder { get; set; } = new();
}
