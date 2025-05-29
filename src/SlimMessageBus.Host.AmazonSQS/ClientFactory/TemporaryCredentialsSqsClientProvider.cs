namespace SlimMessageBus.Host.AmazonSQS;

public class TemporaryCredentialsSqsClientProvider(AmazonSQSConfig clientConfig, string roleArn, string roleSessionName)
    : AbstractTemporaryCredentialsSqsClientProvider<AmazonSQSClient, AmazonSQSConfig>(clientConfig, roleArn, roleSessionName),
    ISqsClientProvider
{
    protected override AmazonSQSClient CreateClient(SessionAWSCredentials temporaryCredentials, AmazonSQSConfig clientConfig)
        => new(temporaryCredentials, clientConfig);
}


