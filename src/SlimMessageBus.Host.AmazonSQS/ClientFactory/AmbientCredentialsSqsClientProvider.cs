namespace SlimMessageBus.Host.AmazonSQS;

public class AmbientCredentialsSqsClientProvider(AmazonSQSConfig sqsConfig)
    : AbstractClientProvider<AmazonSQSClient>(new AmazonSQSClient(sqsConfig)),
    ISqsClientProvider
{
}