namespace SlimMessageBus.Host.Outbox;

public class DefaultInstanceIdProvider : IInstanceIdProvider
{
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public string GetInstanceId() => _instanceId;
}
