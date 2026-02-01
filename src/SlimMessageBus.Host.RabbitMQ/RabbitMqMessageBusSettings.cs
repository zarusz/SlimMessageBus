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

    /// <summary>
    /// Enables RabbitMQ Publisher Confirms. When enabled, the channel will be created in confirm mode
    /// and each <c>BasicPublishAsync</c> call will wait for broker acknowledgement before completing.
    /// This provides guaranteed message delivery at the cost of reduced throughput.
    /// Default is <c>false</c> (opt-in).
    /// </summary>
    /// <remarks>
    /// See https://www.rabbitmq.com/docs/confirms#publisher-confirms for more information.
    /// </remarks>
    public bool EnablePublisherConfirms { get; set; }

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

    /// <summary>
    /// The maximum time to wait for the broker to acknowledge a published message when publisher confirms are enabled.
    /// If the broker does not respond within this timeout, a <see cref="ProducerMessageBusException"/> is thrown.
    /// Only applicable when publisher confirms are enabled (bus-level or producer-level).
    /// Default is 10 seconds. Set to <c>null</c> to disable the timeout and rely solely on the caller's cancellation token.
    /// </summary>
    public TimeSpan? PublisherConfirmsTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

