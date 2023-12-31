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
        var provisioningService = new SqlTopologyService(LoggerFactory.CreateLogger<SqlTopologyService>(), (SqlRepository)sqlRepository, ProviderSettings);
        await provisioningService.Migrate(CancellationToken); // provisining happens asynchronously
    }

    protected override async Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken = default)
    {
        var sqlRepository = targetBus.ServiceProvider.GetService<ISqlRepository>();

        // ToDo: Save to table
    }
}