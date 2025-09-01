namespace SlimMessageBus.Host.AmazonSQS;

public class AmbientCredentialsSnsClientProvider(AmazonSimpleNotificationServiceConfig config)
    : AbstractClientProvider<IAmazonSimpleNotificationService>(new AmazonSimpleNotificationServiceClient(config)),
    ISnsClientProvider
{
}