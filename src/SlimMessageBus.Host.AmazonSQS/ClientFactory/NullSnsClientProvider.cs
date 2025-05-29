namespace SlimMessageBus.Host.AmazonSQS;

internal class NullSnsClientProvider : ISnsClientProvider
{
    public AmazonSimpleNotificationServiceClient Client
        => throw new ConfigurationMessageBusException("The connection to Amazon SNS has not been provided - check your bus configuration");

    public Task EnsureClientAuthenticated() => Task.CompletedTask;
}
