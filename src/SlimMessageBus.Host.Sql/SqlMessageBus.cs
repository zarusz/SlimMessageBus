namespace SlimMessageBus.Host.Sql;

public class SqlMessageBus : RelationalMessageBusBase<SqlMessageBusSettings, SqlTransportMessage, ISqlRepository>
{
    public SqlMessageBus(MessageBusSettings settings, SqlMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
    }

    protected override IMessageBusSettingsValidationService ValidationService => new SqlMessageBusSettingsValidationService(Settings, ProviderSettings);

    protected override Type ConsumerErrorHandlerOpenGenericType => typeof(ISqlConsumerErrorHandler<>);

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology().ConfigureAwait(false);

        var scope = Settings.ServiceProvider.CreateScope();
        try
        {
            var sqlRepository = scope.ServiceProvider.GetRequiredService<ISqlRepository>();
            var sqlTransactionService = scope.ServiceProvider.GetService<ISqlTransactionService>();
            var provisioningService = new SqlTopologyService(LoggerFactory.CreateLogger<SqlTopologyService>(), (SqlRepository)sqlRepository, sqlTransactionService, ProviderSettings);
            await provisioningService.Migrate(CancellationToken).ConfigureAwait(false);

            await ProvisionSubscriptions(sqlRepository).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsyncScope().ConfigureAwait(false);
        }
    }

    protected override string GetSubscriptionName(AbstractConsumerSettings settings)
        => settings.GetSubscriptionName();

    protected override AbstractConsumer CreateConsumer(IEnumerable<AbstractConsumerSettings> consumerSettings, IMessageProcessor<SqlTransportMessage> processor, string path, PathKind pathKind, string subscriptionName, string instanceId)
        => new SqlConsumer(
                LoggerFactory.CreateLogger<SqlConsumer>(),
                consumerSettings,
                Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(),
                Settings.ServiceProvider,
                ProviderSettings,
                processor,
                path,
                pathKind,
                subscriptionName,
                instanceId);

    protected override SqlTransportMessage CreateTransportMessage(IServiceProvider serviceProvider, string path, PathKind pathKind, string subscriptionName, string messageType, byte[] payload, IDictionary<string, object> headers)
    {
        var guidGenerator = ProviderSettings.IdGeneration.GuidGenerator ?? (IGuidGenerator)serviceProvider.GetRequiredService(ProviderSettings.IdGeneration.GuidGeneratorType);
        return new SqlTransportMessage
        {
            Id = ProviderSettings.IdGeneration.Mode == SqlMessageIdGenerationMode.ClientGuidGenerator ? guidGenerator.NewGuid() : Guid.Empty,
            Path = path,
            PathKind = pathKind,
            SubscriptionName = subscriptionName,
            MessageType = messageType,
            MessagePayload = payload,
            Headers = headers != null ? new Dictionary<string, object>(headers) : null
        };
    }
}
