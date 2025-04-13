namespace SlimMessageBus.Host.AmazonSQS;

public class TemporaryCredentialsSnsClientProvider(AmazonSimpleNotificationServiceConfig clientConfig, string roleArn, string roleSessionName)
    : AbstractTemporaryCredentialsSqsClientProvider<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceConfig>(clientConfig, roleArn, roleSessionName),
    ISnsClientProvider
{
    protected override IAmazonSimpleNotificationService CreateClient(SessionAWSCredentials temporaryCredentials, AmazonSimpleNotificationServiceConfig clientConfig)
        => new AmazonSimpleNotificationServiceClient(temporaryCredentials, clientConfig);
}


