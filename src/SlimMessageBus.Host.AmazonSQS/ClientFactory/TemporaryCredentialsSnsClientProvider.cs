namespace SlimMessageBus.Host.AmazonSQS;

public class TemporaryCredentialsSnsClientProvider(AmazonSimpleNotificationServiceConfig clientConfig, string roleArn, string roleSessionName)
    : AbstractTemporaryCredentialsSqsClientProvider<AmazonSimpleNotificationServiceClient, AmazonSimpleNotificationServiceConfig>(clientConfig, roleArn, roleSessionName),
    ISnsClientProvider
{
    protected override AmazonSimpleNotificationServiceClient CreateClient(SessionAWSCredentials temporaryCredentials, AmazonSimpleNotificationServiceConfig clientConfig)
        => new(temporaryCredentials, clientConfig);
}


