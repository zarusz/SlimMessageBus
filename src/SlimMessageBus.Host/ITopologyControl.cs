namespace SlimMessageBus.Host;

public interface ITopologyControl
{
    /// <summary>
    /// Provisions the topology (topics/subscriptions and queues) for the given message bus (if the transport supports it).
    /// </summary>
    /// <returns></returns>
    Task ProvisionTopology();
}