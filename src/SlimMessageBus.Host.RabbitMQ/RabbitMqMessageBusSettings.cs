namespace SlimMessageBus.Host.RabbitMQ;

using SlimMessageBus.Host.Serialization;

public class RabbitMqMessageBusSettings : HasProviderExtensions
{
    /// <summary>
    /// AMQP URI - Sets or retrieves the <see cref="ConnectionFactory.Uri"/>.
    /// </summary>
    public string ConnectionString
    {
        get => ConnectionFactory.Uri?.ToString();
        set => ConnectionFactory.Uri = new Uri(value);
    }

    public ConnectionFactory ConnectionFactory { get; set; } = new()
    {
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
    };

    public IList<AmqpTcpEndpoint> Endpoints { get; set; } = new List<AmqpTcpEndpoint>();

    public IMessageSerializer HeaderSerializer { get; set; } = new DefaultRabbitMqHeaderSerializer();
}
