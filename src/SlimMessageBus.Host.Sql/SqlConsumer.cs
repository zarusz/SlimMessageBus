namespace SlimMessageBus.Host.Sql;

public class SqlConsumer : RelationalConsumerBase<SqlTransportMessage, ISqlRepository>
{
    public SqlConsumer(
        ILogger<SqlConsumer> logger,
        IEnumerable<AbstractConsumerSettings> consumerSettings,
        IEnumerable<IAbstractConsumerInterceptor> interceptors,
        IServiceProvider serviceProvider,
        SqlMessageBusSettings providerSettings,
        IMessageProcessor<SqlTransportMessage> messageProcessor,
        string path,
        PathKind pathKind,
        string subscriptionName,
        string instanceId)
        : base(logger, consumerSettings, interceptors, serviceProvider, providerSettings, messageProcessor, path, pathKind, subscriptionName, instanceId, "SQL")
    {
    }
}
