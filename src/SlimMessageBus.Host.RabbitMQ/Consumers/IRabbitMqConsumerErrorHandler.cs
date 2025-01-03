namespace SlimMessageBus.Host.RabbitMQ;

public interface IRabbitMqConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class RabbitMqConsumerErrorHandler<T> : ConsumerErrorHandler<T>, IRabbitMqConsumerErrorHandler<T>
{
    public virtual ProcessResult Requeue() => RabbitMqProcessResult.Requeue;
}

public record RabbitMqProcessResult : ProcessResult
{
    /// <summary>
    /// The message should be placed back into the queue.
    /// </summary>
    public static readonly ProcessResult Requeue = new RequeueState();

    public record RequeueState() : ProcessResult();
}