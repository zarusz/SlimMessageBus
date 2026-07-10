namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlMessageBus : RelationalMessageBusBase<PostgreSqlMessageBusSettings, PostgreSqlTransportMessage, IPostgreSqlRepository>
{
    public PostgreSqlMessageBus(MessageBusSettings settings, PostgreSqlMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
    }

    protected override IMessageBusSettingsValidationService ValidationService => new PostgreSqlMessageBusSettingsValidationService(Settings, ProviderSettings);

    protected override Type ConsumerErrorHandlerOpenGenericType => typeof(IPostgreSqlConsumerErrorHandler<>);

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology().ConfigureAwait(false);

        var scope = Settings.ServiceProvider.CreateScope();
        try
        {
            var repository = (PostgreSqlRepository)scope.ServiceProvider.GetRequiredService<IPostgreSqlRepository>();
            var template = scope.ServiceProvider.GetRequiredService<PostgreSqlTemplate>();
            var topologyService = new PostgreSqlTopologyService(LoggerFactory.CreateLogger<PostgreSqlTopologyService>(), ProviderSettings, repository, template);

            await topologyService.Migrate(CancellationToken).ConfigureAwait(false);

            await ProvisionSubscriptions(repository).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsyncScope().ConfigureAwait(false);
        }
    }

    protected override string GetSubscriptionName(AbstractConsumerSettings settings)
        => settings.GetSubscriptionName();

    protected override AbstractConsumer CreateConsumer(IEnumerable<AbstractConsumerSettings> consumerSettings, IMessageProcessor<PostgreSqlTransportMessage> processor, string path, PathKind pathKind, string subscriptionName, string instanceId)
        => new PostgreSqlConsumer(
                LoggerFactory.CreateLogger<PostgreSqlConsumer>(),
                consumerSettings,
                Settings.ServiceProvider.GetServices<IAbstractConsumerInterceptor>(),
                Settings.ServiceProvider,
                ProviderSettings,
                processor,
                path,
                pathKind,
                subscriptionName,
                instanceId);

    protected override PostgreSqlTransportMessage CreateTransportMessage(IServiceProvider serviceProvider, string path, PathKind pathKind, string subscriptionName, string messageType, byte[] payload, IDictionary<string, object> headers)
    {
        var guidGenerator = ProviderSettings.IdGeneration.GuidGenerator ?? (IGuidGenerator)serviceProvider.GetRequiredService(ProviderSettings.IdGeneration.GuidGeneratorType);
        return new PostgreSqlTransportMessage
        {
            Id = ProviderSettings.IdGeneration.Mode == PostgreSqlMessageIdGenerationMode.ClientGuidGenerator ? guidGenerator.NewGuid() : Guid.Empty,
            Path = path,
            PathKind = pathKind,
            SubscriptionName = subscriptionName,
            MessageType = messageType,
            MessagePayload = payload,
            Headers = headers != null ? new Dictionary<string, object>(headers) : null
        };
    }

    protected override async Task AfterProduce(IPostgreSqlRepository repository, string path, CancellationToken cancellationToken)
    {
        if (repository is PostgreSqlRepository postgreSqlRepository)
        {
            await postgreSqlRepository.Notify(path, cancellationToken).ConfigureAwait(false);
        }
    }
}
