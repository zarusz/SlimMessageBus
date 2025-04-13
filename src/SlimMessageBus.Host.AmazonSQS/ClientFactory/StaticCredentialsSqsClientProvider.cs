namespace SlimMessageBus.Host.AmazonSQS;

public class StaticCredentialsSqsClientProvider(AmazonSQSConfig sqsConfig, AWSCredentials credentials)
    : AbstractClientProvider<AmazonSQSClient>(new AmazonSQSClient(credentials, sqsConfig)),
    ISqsClientProvider
{
}
