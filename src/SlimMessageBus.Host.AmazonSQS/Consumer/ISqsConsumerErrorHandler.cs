namespace SlimMessageBus.Host.AmazonSQS;

public interface ISqsConsumerErrorHandler<in T> : IConsumerErrorHandler<T>
{
}
