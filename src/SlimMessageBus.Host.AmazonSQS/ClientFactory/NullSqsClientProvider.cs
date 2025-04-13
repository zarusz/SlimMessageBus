namespace SlimMessageBus.Host.AmazonSQS;

internal class NullSqsClientProvider : ISqsClientProvider
{
    public IAmazonSQS Client
        => throw new ConfigurationMessageBusException("The connection to Amazon SQS has not been provided - check your bus configuration");

    public Task EnsureClientAuthenticated() => Task.CompletedTask;
}