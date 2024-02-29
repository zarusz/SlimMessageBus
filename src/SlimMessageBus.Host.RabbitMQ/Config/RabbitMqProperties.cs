namespace SlimMessageBus.Host.RabbitMQ;

static internal class RabbitMqProperties
{
    public static readonly string ExchangeType = $"RabbitMQ_{nameof(ExchangeType)}";
    public static readonly string ExchangeArguments = $"RabbitMQ_{nameof(ExchangeArguments)}";
    public static readonly string ExchangeDurable = $"RabbitMQ_{nameof(ExchangeDurable)}";
    public static readonly string ExchangeAutoDelete = $"RabbitMQ_{nameof(ExchangeAutoDelete)}";

    public static readonly string QueueName = $"RabbitMQ_{nameof(QueueName)}";
    public static readonly string QueueDurable = $"RabbitMQ_{nameof(QueueDurable)}";
    public static readonly string QueueAutoDelete = $"RabbitMQ_{nameof(QueueAutoDelete)}";
    public static readonly string QueueExclusive = $"RabbitMQ_{nameof(QueueExclusive)}";
    public static readonly string QueueArguments = $"RabbitMQ_{nameof(QueueArguments)}";

    public static readonly string DeadLetterExchange = $"RabbitMQ_{nameof(DeadLetterExchange)}";
    public static readonly string DeadLetterExchangeType = $"RabbitMQ_{nameof(DeadLetterExchangeType)}";
    public static readonly string DeadLetterExchangeDurable = $"RabbitMQ_{nameof(DeadLetterExchangeDurable)}";
    public static readonly string DeadLetterExchangeAutoDelete = $"RabbitMQ_{nameof(DeadLetterExchangeAutoDelete)}";
    public static readonly string DeadLetterExchangeRoutingKey = $"RabbitMQ_{nameof(DeadLetterExchangeRoutingKey)}";

    public static readonly string BindingRoutingKey = $"RabbitMQ_{nameof(BindingRoutingKey)}";

    public static readonly string MessageRoutingKeyProvider = $"RabbitMQ_{nameof(MessageRoutingKeyProvider)}";
    public static readonly string MessagePropertiesModifier = $"RabbitMQ_{nameof(MessagePropertiesModifier)}";

    public static readonly string TopologyInitializer = $"RabbitMQ_{nameof(TopologyInitializer)}";

    public static readonly string Message = $"RabbitMQ_{nameof(Message)}";

    public static readonly string ProvderSettings = $"RabbitMQ_{nameof(ProvderSettings)}";

    public static readonly string MessageAcknowledgementMode = $"RabbitMQ_{nameof(MessageAcknowledgementMode)}";
}
