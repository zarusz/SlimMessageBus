namespace SlimMessageBus.Host.AmazonSQS;

public class AmbientCredentialsSqsClientProvider(AmazonSQSConfig sqsConfig)
    : AbstractClientProvider<IAmazonSQS>(new AmazonSQSClient(sqsConfig)),
    ISqsClientProvider
{
}