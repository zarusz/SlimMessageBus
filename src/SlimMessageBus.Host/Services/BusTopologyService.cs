namespace SlimMessageBus.Host.Services;

public abstract class BusTopologyService<TProvider>
{
    public ILogger Logger { get; }
    public MessageBusSettings Settings { get; }
    public TProvider ProviderSettings { get; }

    public BusTopologyService(
        ILogger logger,
        MessageBusSettings settings,
        TProvider providerSettings)
    {
        Logger = logger;
        Settings = settings;
        ProviderSettings = providerSettings;
    }

    public async Task ProvisionTopology()
    {
        Logger.LogInformation("Provisioning topology for {BusName} bus...", Settings.Name);
        try
        {
            await OnProvisionTopology();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Could not provision topology for {BusName}", Settings.Name);
        }
        finally
        {
            Logger.LogInformation("Provisioning topology for {BusName} bus completed", Settings.Name);
        }
    }

    protected abstract Task OnProvisionTopology();
}
