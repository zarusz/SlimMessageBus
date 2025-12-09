namespace SlimMessageBus.Host.RabbitMQ;

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
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        // By default the consumer dispatch is single threaded, we can increase it to the number of consumers by applying the .Instances(10) setting
        ConsumerDispatchConcurrency = 1
    };

    public IList<AmqpTcpEndpoint> Endpoints { get; set; } = [];

    /// <summary>
    /// Allows to set a custom header values converter between SMB and the underlying RabbitMq client.
    /// See the <see cref="DefaultHeaderValueConverter"/>.
    /// </summary>
    public IHeaderValueConverter HeaderValueConverter { get; set; } = new DefaultHeaderValueConverter();

    /// <summary>
    /// Allows to handle messages that arrive with an unrecognized routing key and decide what to do with them.
    /// By default the message is Acknowledged.
    /// </summary>
    public RabbitMqMessageUnrecognizedRoutingKeyHandler MessageUnrecognizedRoutingKeyHandler { get; set; } = (_) => RabbitMqMessageConfirmOptions.Ack;
}

