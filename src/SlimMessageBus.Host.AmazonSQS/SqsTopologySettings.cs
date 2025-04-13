namespace SlimMessageBus.Host.AmazonSQS;

using Amazon.SimpleNotificationService.Model;

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
    /// Default configuration to be applied when a queue needs to be created (<see cref="CreateQueueRequest"/>).
    /// </summary>
    public Action<CreateQueueRequest> CreateQueueOptions { get; set; }

    /// <summary>
    /// A filter that allows (or not) for declared producers to provision needed topics. True by default.
    /// </summary>
    public bool CanProducerCreateTopic { get; set; } = true;
    /// <summary>
    /// A filter that allows (or not) for declared consumers to provision needed topics. True by default.
    /// </summary>
    public bool CanConsumerCreateTopic { get; set; } = true;
    /// <summary>
    /// Default configuration to be applied when a topic needs to be created (<see cref="CreateTopicRequest"/>).
    /// </summary>
    public Action<CreateTopicRequest> CreateTopicOptions { get; set; }
    /// <summary>
    /// A filter that allows (or not) for declared consumers to provision needed subscription. True by default.
    /// </summary>
    public bool CanConsumerCreateTopicSubscription { get; set; } = true;
    /// <summary>
    /// Default configuration to be applied when a topic needs to be created (<see cref="SubscribeRequest"/>).
    /// </summary>
    public Action<SubscribeRequest> CreateSubscriptionOptions { get; set; }

    /// <summary>
    /// Interceptor that allows to intercept the topology provisioning process (SQS and SNS) and to apply custom logic before and after the provisioning process.
    /// </summary>
    public SqsTopologyInterceptor OnProvisionTopology { get; set; } = (clientSqs, clientSns, provision, ct) => provision();
}

/// <summary>
/// Interceptor that allows to intercept the topology provisioning process and to apply custom logic before and after the provisioning process. 
/// </summary>
/// <param name="clientSqs">The SQS client</param>
/// <param name="clientSns">The SNS client</param>
/// <param name="provision">Delegate allowing to perform topology provisioning</param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
public delegate Task SqsTopologyInterceptor(IAmazonSQS clientSqs, IAmazonSimpleNotificationService clientSns, Func<Task> provision, CancellationToken cancellationToken);
