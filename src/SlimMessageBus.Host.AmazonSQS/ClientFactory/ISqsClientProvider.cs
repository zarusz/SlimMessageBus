namespace SlimMessageBus.Host.AmazonSQS;

/// <summary>
/// Wrapper for the <see cref="AmazonSQSClient"/> and the authentication strategy.
/// </summary>
public interface ISqsClientProvider
{
    IAmazonSQS Client { get; }
    Task EnsureClientAuthenticated();
}


