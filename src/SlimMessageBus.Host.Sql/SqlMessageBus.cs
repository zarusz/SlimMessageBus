namespace SlimMessageBus.Host.Sql;

public class SqlMessageBus : MessageBusBase<SqlMessageBusSettings>
{
    public SqlMessageBus(MessageBusSettings settings, SqlMessageBusSettings providerSettings)
        : base(settings, providerSettings)
    {
    }

    protected override void Build()
    {
        base.Build();

        AddInit(ProvisionTopology());
    }

    public override async Task ProvisionTopology()
    {
        await base.ProvisionTopology();

        using var scope = Settings.ServiceProvider.CreateScope();
        var sqlRepository = scope.ServiceProvider.GetService<ISqlRepository>();
        var sqlTransactionService = scope.ServiceProvider.GetService<ISqlTransactionService>();
        var provisioningService = new SqlTopologyService(LoggerFactory.CreateLogger<SqlTopologyService>(), (SqlRepository)sqlRepository, sqlTransactionService, ProviderSettings);
        await provisioningService.Migrate(CancellationToken); // provisioning happens asynchronously
    }

    public override Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<ProduceToTransportBulkResult<T>> ProduceToTransportBulk<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        var sqlRepository = targetBus.ServiceProvider.GetService<ISqlRepository>();

        // ToDo: Save to table

        return Task.FromResult(new ProduceToTransportBulkResult<T>([], new NotImplementedException()));
    }
}