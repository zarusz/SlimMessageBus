namespace SlimMessageBus.Host.AzureServiceBus;

public interface IServiceBusConsumerErrorHandler<in T> : IConsumerErrorHandler<T>;

public abstract class ServiceBusConsumerErrorHandler<T> : ConsumerErrorHandler<T>, IServiceBusConsumerErrorHandler<T>
{
    public ProcessResult DeadLetter(string reason = null, string description = null)
    {
        return reason == null && description == null
            ? ServiceBusProcessResult.DeadLetter
            : new ServiceBusProcessResult.DeadLetterState(reason, description);
    }

    public ProcessResult Failure(IReadOnlyDictionary<string, object> properties)
    {
        return properties != null && properties.Count > 0
            ? new ServiceBusProcessResult.FailureStateWithProperties(properties)
            : Failure();
    }
}

public record ServiceBusProcessResult : ProcessResult
{
    /// <summary>
    /// The message must be sent to the dead letter queue.
    /// </summary>
    public static readonly ProcessResult DeadLetter = new DeadLetterState();

    public record DeadLetterState : ProcessResult
    {
        public DeadLetterState(string reason = null, string description = null)
        {
            Reason = reason;
            Description = description;
        }

        public string Reason { get; }
        public string Description { get; }
    }

    public record FailureStateWithProperties : FailureState
    {
        public FailureStateWithProperties(IReadOnlyDictionary<string, object> properties)
        {
            Properties = properties;
        }

        public IReadOnlyDictionary<string, object> Properties { get; }
    }
}