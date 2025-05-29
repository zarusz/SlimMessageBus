namespace SlimMessageBus.Host.AmazonSQS;

public class StaticCredentialsSnsClientProvider(AmazonSimpleNotificationServiceConfig config, AWSCredentials credentials)
    : AbstractClientProvider<AmazonSimpleNotificationServiceClient>(new AmazonSimpleNotificationServiceClient(credentials, config)),
    ISnsClientProvider
{
}