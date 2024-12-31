namespace SlimMessageBus.Host.AmazonSQS;

public interface ISqsConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class SqsConsumerErrorHandler<T> : ConsumerErrorHandler<T>, ISqsConsumerErrorHandler<T>;