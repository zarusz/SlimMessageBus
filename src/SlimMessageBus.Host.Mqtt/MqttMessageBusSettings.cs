namespace SlimMessageBus.Host.Mqtt;

public class MqttMessageBusSettings
{
    /// <summary>
    /// Factory for creating the underlying <see cref="MqttClient"/>.
    /// </summary>
    public MqttClientFactory ClientFactory { get; set; } = new();
    /// <summary>
    /// Used to build the underlying <see cref="MqttClient"/> options.
    /// </summary>
    public MqttClientOptionsBuilder ClientBuilder { get; set; } = new();
    /// <summary>
    /// Delay before a reconnect attempt is made after an unexpected disconnection.
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}
