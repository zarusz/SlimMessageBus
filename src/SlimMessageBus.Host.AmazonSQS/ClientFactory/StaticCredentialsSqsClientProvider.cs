namespace SlimMessageBus.Host.AmazonSQS;

public class StaticCredentialsSqsClientProvider(AmazonSQSConfig sqsConfig, AWSCredentials credentials)
    : AbstractClientProvider<IAmazonSQS>(new AmazonSQSClient(credentials, sqsConfig)),
    ISqsClientProvider
{
}
