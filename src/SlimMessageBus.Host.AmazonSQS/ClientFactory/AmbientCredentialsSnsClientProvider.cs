namespace SlimMessageBus.Host.AmazonSQS;

public class AmbientCredentialsSnsClientProvider(AmazonSimpleNotificationServiceConfig config)
    : AbstractClientProvider<AmazonSimpleNotificationServiceClient>(new AmazonSimpleNotificationServiceClient(config)),
    ISnsClientProvider
{
}