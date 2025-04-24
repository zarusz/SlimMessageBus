namespace SlimMessageBus.Host.RabbitMQ;

static internal class RabbitMqProperties
{
    public static readonly ProviderExtensionProperty<string> ExchangeType = new($"RabbitMQ_{nameof(ExchangeType)}");
    public static readonly ProviderExtensionProperty<IDictionary<string, object>> ExchangeArguments = new($"RabbitMQ_{nameof(ExchangeArguments)}");
    public static readonly ProviderExtensionProperty<bool> ExchangeDurable = new($"RabbitMQ_{nameof(ExchangeDurable)}");
    public static readonly ProviderExtensionProperty<bool> ExchangeAutoDelete = new($"RabbitMQ_{nameof(ExchangeAutoDelete)}");

    public static readonly ProviderExtensionProperty<string> QueueName = new($"RabbitMQ_{nameof(QueueName)}");
    public static readonly ProviderExtensionProperty<bool> QueueDurable = new($"RabbitMQ_{nameof(QueueDurable)}");
    public static readonly ProviderExtensionProperty<bool> QueueAutoDelete = new($"RabbitMQ_{nameof(QueueAutoDelete)}");
    public static readonly ProviderExtensionProperty<bool> QueueExclusive = new($"RabbitMQ_{nameof(QueueExclusive)}");
    public static readonly ProviderExtensionProperty<IDictionary<string, object>> QueueArguments = new($"RabbitMQ_{nameof(QueueArguments)}");

    public static readonly ProviderExtensionProperty<string> DeadLetterExchange = new($"RabbitMQ_{nameof(DeadLetterExchange)}");
    public static readonly ProviderExtensionProperty<string> DeadLetterExchangeType = new($"RabbitMQ_{nameof(DeadLetterExchangeType)}");
    public static readonly ProviderExtensionProperty<bool> DeadLetterExchangeDurable = new($"RabbitMQ_{nameof(DeadLetterExchangeDurable)}");
    public static readonly ProviderExtensionProperty<bool> DeadLetterExchangeAutoDelete = new($"RabbitMQ_{nameof(DeadLetterExchangeAutoDelete)}");
    public static readonly ProviderExtensionProperty<string> DeadLetterExchangeRoutingKey = new($"RabbitMQ_{nameof(DeadLetterExchangeRoutingKey)}");

    public static readonly ProviderExtensionProperty<string> BindingRoutingKey = new($"RabbitMQ_{nameof(BindingRoutingKey)}");

    public static readonly ProviderExtensionProperty<RabbitMqMessageRoutingKeyProvider<object>> MessageRoutingKeyProvider = new($"RabbitMQ_{nameof(MessageRoutingKeyProvider)}");
    public static readonly ProviderExtensionProperty<RabbitMqMessagePropertiesModifier<object>> MessagePropertiesModifier = new($"RabbitMQ_{nameof(MessagePropertiesModifier)}");

    public static readonly ProviderExtensionProperty<RabbitMqTopologyInitializer> TopologyInitializer = new($"RabbitMQ_{nameof(TopologyInitializer)}");

    public static readonly ProviderExtensionProperty<BasicDeliverEventArgs> Message = new($"RabbitMQ_{nameof(Message)}");

    public static readonly ProviderExtensionProperty<RabbitMqMessageBusSettings> ProviderSettings = new($"RabbitMQ_{nameof(ProviderSettings)}");

    public static readonly ProviderExtensionProperty<RabbitMqMessageAcknowledgementMode?> MessageAcknowledgementMode = new($"RabbitMQ_{nameof(MessageAcknowledgementMode)}");
}
