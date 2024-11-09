namespace SlimMessageBus.Host.AmazonSQS;
public class SqsMessageBusSettings
{
    /// <summary>
    /// The factory method to create the client provider <see cref="ISqsClientProvider"/> which is used to manage the <see cref="AmazonSQSClient"/>.
    /// </summary>
    public Func<IServiceProvider, ISqsClientProvider> ClientProviderFactory { get; set; }

    /// <summary>
    /// The configuration for the SQS client.
    /// </summary>
    public AmazonSQSConfig SqsClientConfig { get; set; } = new();

    /// <summary>
    /// Serializer used to serialize SQS message header values.
    /// By default the <see cref="DefaultSqsHeaderSerializer"/> is used.
    /// </summary>
    public ISqsHeaderSerializer SqsHeaderSerializer { get; set; } = new DefaultSqsHeaderSerializer();

    /// <summary>
    /// Settings for auto creation of queues if they don't exist.
    /// </summary>
    public SqsTopologySettings TopologyProvisioning { get; set; } = new();

    /// <summary>
    /// Connect to AWS using long term credentials.
    /// See https://docs.aws.amazon.com/sdkref/latest/guide/access-iam-users.html
    /// </summary>
    /// <param name="accessKey"></param>
    /// <param name="secretKey"></param>
    /// <returns></returns>
    public SqsMessageBusSettings UseCredentials(string accessKey, string secretKey)
    {
        ClientProviderFactory = (svp) => new StaticCredentialsSqsClientProvider(SqsClientConfig, new BasicAWSCredentials(accessKey, secretKey));
        return this;
    }

    /// <summary>
    /// Connect to AWS using temporary credentials (recommended)
    /// See https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_temp_use-resources.html#RequestWithSTS
    /// </summary>
    /// <param name="roleArn"></param>
    /// <param name="roleSessionName"></param>
    /// <returns></returns>
    public SqsMessageBusSettings UseTemporaryCredentials(string roleArn, string roleSessionName)
    {
        ClientProviderFactory = (svp) => new TemporaryCredentialsSqsClientProvider(SqsClientConfig, roleArn, roleSessionName);
        return this;
    }

    /// <summary>
    /// Sets the region for the SQS client.
    /// </summary>
    /// <param name="region"></param>
    /// <returns></returns>
    public SqsMessageBusSettings UseRegion(RegionEndpoint region)
    {
        SqsClientConfig.RegionEndpoint = region;
        return this;
    }

    /// <summary>
    /// Maximum message count to be recieved by the consumer in one batch (1-10). Default is 10.
    /// </summary>
    public int MaxMessageCount { get; set; } = 10;

}
