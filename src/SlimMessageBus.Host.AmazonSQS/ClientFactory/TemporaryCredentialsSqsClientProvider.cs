namespace SlimMessageBus.Host.AmazonSQS;

public class TemporaryCredentialsSqsClientProvider(AmazonSQSConfig clientConfig, string roleArn, string roleSessionName)
    : AbstractTemporaryCredentialsSqsClientProvider<IAmazonSQS, AmazonSQSConfig>(clientConfig, roleArn, roleSessionName),
    ISqsClientProvider
{
    protected override IAmazonSQS CreateClient(SessionAWSCredentials temporaryCredentials, AmazonSQSConfig clientConfig)
        => new AmazonSQSClient(temporaryCredentials, clientConfig);
}


