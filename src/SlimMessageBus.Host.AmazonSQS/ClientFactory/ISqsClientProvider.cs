namespace SlimMessageBus.Host.AmazonSQS;

public interface ISqsClientProvider
{
    AmazonSQSClient Client { get; }
    Task EnsureClientAuthenticated();
}


