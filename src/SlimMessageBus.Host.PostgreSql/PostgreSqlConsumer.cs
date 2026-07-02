namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlConsumer : RelationalConsumerBase<PostgreSqlTransportMessage, IPostgreSqlRepository>
{
    public PostgreSqlConsumer(
        ILogger<PostgreSqlConsumer> logger,
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        IEnumerable<IAbstractConsumerInterceptor> interceptors,
        IServiceProvider serviceProvider,
        PostgreSqlMessageBusSettings providerSettings,
        IMessageProcessor<PostgreSqlTransportMessage> messageProcessor,
        string path,
        PathKind pathKind,
        string? subscriptionName,
        string instanceId)
        : base(logger, consumerSettings, interceptors, serviceProvider, providerSettings, messageProcessor, path, pathKind, subscriptionName, instanceId, "PostgreSQL")
    {
    }
}
