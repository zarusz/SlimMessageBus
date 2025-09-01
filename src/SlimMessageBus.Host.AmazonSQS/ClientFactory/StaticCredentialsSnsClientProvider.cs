namespace SlimMessageBus.Host.AmazonSQS;

public class StaticCredentialsSnsClientProvider(AmazonSimpleNotificationServiceConfig config, AWSCredentials credentials)
    : AbstractClientProvider<IAmazonSimpleNotificationService>(new AmazonSimpleNotificationServiceClient(credentials, config)),
    ISnsClientProvider
{
}
