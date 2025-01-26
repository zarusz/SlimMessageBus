namespace SlimMessageBus.Host.AzureServiceBus;

public interface IServiceBusConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class ServiceBusConsumerErrorHandler<T> : ConsumerErrorHandler<T>, IServiceBusConsumerErrorHandler<T>
{
    public ProcessResult DeadLetter() => ServiceBusProcessResult.DeadLetter;
}

public record ServiceBusProcessResult : ProcessResult
{
    /// <summary>
    /// The message must be sent to the dead letter queue.
    /// </summary>
    public static readonly ProcessResult DeadLetter = new DeadLetterState();

    public record DeadLetterState() : ProcessResult();
}