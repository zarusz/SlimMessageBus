namespace SlimMessageBus.Host.AmazonSQS;

public class SqsTopologySettings
{
    /// <summary>
    /// Indicates whether topology provisioning is enabled. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared producers to provision needed queues. True by default.
    /// </summary>
    public bool CanProducerCreateQueue { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to provision needed queues. True by default.
    /// </summary>
    public bool CanConsumerCreateQueue { get; set; } = true;
    /// <summary>
    /// Default configuration to be applied when a topic needs to be created (<see cref="CreateQueueRequest"/>).
    /// </summary>
    public Action<CreateQueueRequest> CreateQueueOptions { get; set; }

    /// <summary>
    /// Interceptor that allows to intercept the topology provisioning process.
    /// </summary>
    public SqsTopologyInterceptor OnProvisionTopology { get; set; } = (client, provision) => provision();
}

/// <summary>
/// Interceptor that allows to intercept the topology provisioning process and to apply custom logic before and after the provisioning process. 
/// </summary>
/// <param name="client">The SQS client</param>
/// <param name="provision">Delegate allowing to perform topology provisioning</param>
/// <returns></returns>
public delegate Task SqsTopologyInterceptor(AmazonSQSClient client, Func<Task> provision);
