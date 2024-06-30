namespace SlimMessageBus.Host.Kafka;

public class KafkaBusTopologySettings
{
    /// <summary>
    /// Indicates whether topology provisioning is enabled. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;
    public KafkaBusTopologyInterceptor OnProvisionTopology { get; set; } = (client, provision) => provision();
}

/// <summary>
/// Interceptor that allows to intercept the topology provisioning process and to apply custom logic before and after the provisioning process. 
/// </summary>
/// <param name="client">Kafka admin client</param>
/// <param name="provision">Delegate allowing to perform topology provisioning</param>
/// <returns></returns>
public delegate Task KafkaBusTopologyInterceptor(IAdminClient client, Func<Task> provision);