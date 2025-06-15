namespace SlimMessageBus.Host.AmazonSQS;

/// <summary>
/// Wrapper for the <see cref="AmazonSimpleNotificationServiceClient"/> and the authentication strategy.
/// </summary>
public interface ISnsClientProvider
{
    AmazonSimpleNotificationServiceClient Client { get; }
    Task EnsureClientAuthenticated();
}
