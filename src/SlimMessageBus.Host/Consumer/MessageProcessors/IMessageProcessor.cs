namespace SlimMessageBus.Host;

public readonly struct ProcessMessageResult(Exception exception, AbstractConsumerSettings consumerSettings, object response, object message)
{
    public Exception Exception { get; init; } = exception;
    public AbstractConsumerSettings ConsumerSettings { get; init; } = consumerSettings;
    public object Response { get; init; } = response;
    public object Message { get; init; } = message;
}

public interface IMessageProcessor<in TMessage>
{
    IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings { get; }

    /// <summary>
    /// Processes the arrived message
    /// </summary>
    /// <returns>Null, if message processing was sucessful, otherwise the Exception</returns>
    Task<ProcessMessageResult> ProcessMessage(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default);
}