namespace SlimMessageBus.Host.AmazonSQS;

public class SqsMessageBusSettings
{
    /// <summary>
    /// The factory method to create the client provider <see cref="ISqsClientProvider"/> which is used to manage the <see cref="AmazonSQSClient"/>.
    /// </summary>
    public Func<IServiceProvider, ISqsClientProvider> SqsClientProviderFactory { get; set; } = (svp) => new NullSqsClientProvider();

    /// <summary>
    /// The configuration for the SQS client.
    /// </summary>
    public AmazonSQSConfig SqsClientConfig { get; set; } = new();

    /// <summary>
    /// The factory method to create the client provider <see cref="ISnsClientProvider"/> which is used to manage the <see cref="AmazonSimpleNotificationServiceClient"/>.
    /// </summary>
    public Func<IServiceProvider, ISnsClientProvider> SnsClientProviderFactory { get; set; } = (svp) => new NullSnsClientProvider();

    /// <summary>
    /// The configuration for the SNS client.
    /// </summary>
    public AmazonSimpleNotificationServiceConfig SnsClientConfig { get; set; } = new();

    /// <summary>
    /// Serializer used to serialize SQS message header values.
    /// By default the <see cref="DefaultSqsHeaderSerializer"/> is used.
    /// </summary>
    public ISqsHeaderSerializer<Amazon.SQS.Model.MessageAttributeValue> SqsHeaderSerializer { get; set; } = new DefaultSqsHeaderSerializer();

    /// <summary>
    /// Serializer used to serialize SNS message header values.
    /// By default the <see cref="DefaultSnsHeaderSerializer"/> is used.
    /// </summary>
    public ISqsHeaderSerializer<Amazon.SimpleNotificationService.Model.MessageAttributeValue> SnsHeaderSerializer { get; set; } = new DefaultSnsHeaderSerializer();

    /// <summary>
    /// Settings for auto creation of queues if they don't exist.
    /// </summary>
    public SqsTopologySettings TopologyProvisioning { get; set; } = new();

    /// <summary>
    /// Maximum message count to be recieved by the consumer in one batch (1-10). Default is 10.
    /// </summary>
    public int MaxMessageCount { get; set; } = 10;

    /// <summary>
    /// Connect to AWS using ambient credentials that are pulled from the environment variables of the AWS host (e.g. Fargate, ECS, EC2, etc.).
    /// </summary>
    /// <param name="mode">Should the credentials apply for SQS or SNS service (or both)</param>
    /// <returns></returns>
    public SqsMessageBusSettings UseAmbientCredentials(SqsMessageBusModes mode = SqsMessageBusModes.All)
    {
        if ((mode & SqsMessageBusModes.Sqs) != 0)
        {
            SqsClientProviderFactory = (svp) => new AmbientCredentialsSqsClientProvider(SqsClientConfig);
        }
        if ((mode & SqsMessageBusModes.Sns) != 0)
        {
            SnsClientProviderFactory = (svp) => new AmbientCredentialsSnsClientProvider(SnsClientConfig);
        }
        return this;
    }

    /// <summary>
    /// Connect to AWS using long term credentials.
    /// See https://docs.aws.amazon.com/sdkref/latest/guide/access-iam-users.html
    /// </summary>
    /// <param name="accessKey"></param>
    /// <param name="secretKey"></param>
    /// <param name="mode">Should the credentials apply for SQS or SNS service (or both)</param>
    /// <returns></returns>
    [Obsolete("The method has been renamed to .UseStaticCredentials()")]
    public SqsMessageBusSettings UseCredentials(string accessKey, string secretKey, SqsMessageBusModes mode = SqsMessageBusModes.All)
        => UseStaticCredentials(accessKey, secretKey, mode);

    /// <summary>
    /// Connect to AWS using long term credentials.
    /// See https://docs.aws.amazon.com/sdkref/latest/guide/access-iam-users.html
    /// </summary>
    /// <param name="accessKey"></param>
    /// <param name="secretKey"></param>
    /// <param name="mode">Should the credentials apply for SQS or SNS service (or both)</param>
    /// <returns></returns>
    public SqsMessageBusSettings UseStaticCredentials(string accessKey, string secretKey, SqsMessageBusModes mode = SqsMessageBusModes.All)
    {
        var credentials = new BasicAWSCredentials(accessKey, secretKey);

        if ((mode & SqsMessageBusModes.Sqs) != 0)
        {
            SqsClientProviderFactory = (svp) => new StaticCredentialsSqsClientProvider(SqsClientConfig, credentials);
        }
        if ((mode & SqsMessageBusModes.Sns) != 0)
        {
            SnsClientProviderFactory = (svp) => new StaticCredentialsSnsClientProvider(SnsClientConfig, credentials);
        }
        return this;
    }

    /// <summary>
    /// Connect to AWS using temporary credentials (recommended)
    /// See https://docs.aws.amazon.com/IAM/latest/UserGuide/id_credentials_temp_use-resources.html#RequestWithSTS
    /// </summary>
    /// <param name="roleArn"></param>
    /// <param name="roleSessionName"></param>
    /// <param name="mode">Should the credentials apply for SQS or SNS service (or both)</param>
    /// <returns></returns>
    public SqsMessageBusSettings UseTemporaryCredentials(string roleArn, string roleSessionName, SqsMessageBusModes mode = SqsMessageBusModes.All)
    {
        if ((mode & SqsMessageBusModes.Sqs) != 0)
        {
            SqsClientProviderFactory = (svp) => new TemporaryCredentialsSqsClientProvider(SqsClientConfig, roleArn, roleSessionName);
        }
        if ((mode & SqsMessageBusModes.Sns) != 0)
        {
            SnsClientProviderFactory = (svp) => new TemporaryCredentialsSnsClientProvider(SnsClientConfig, roleArn, roleSessionName);
        }
        return this;
    }

    /// <summary>
    /// Sets the region for the SQS and SNS client.
    /// </summary>
    /// <param name="region"></param>
    /// <param name="mode">Should the credentials apply for SQS or SNS service (or both)</param>
    /// <returns></returns>
    public SqsMessageBusSettings UseRegion(RegionEndpoint region, SqsMessageBusModes mode = SqsMessageBusModes.All)
    {
        if ((mode & SqsMessageBusModes.Sqs) != 0)
        {
            SqsClientConfig.RegionEndpoint = region;
        }
        if ((mode & SqsMessageBusModes.Sns) != 0)
        {
            SnsClientConfig.RegionEndpoint = region;
        }
        return this;
    }

    public SqsMessageBusSettings()
    {
        // If not specified assume the ambient credentials are used.
        UseAmbientCredentials(SqsMessageBusModes.All);
    }
}
