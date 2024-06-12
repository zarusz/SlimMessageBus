namespace SlimMessageBus.Host.Sql;

using Microsoft.Extensions.DependencyInjection;

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
        await provisioningService.Migrate(CancellationToken); // provisining happens asynchronously
    }

    protected override async Task<(IReadOnlyCollection<T> Dispatched, Exception Exception)> ProduceToTransport<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken = default)
    {
        var sqlRepository = targetBus.ServiceProvider.GetService<ISqlRepository>();

        // ToDo: Save to table

        return ([], new NotImplementedException());
    }
}