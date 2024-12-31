namespace SlimMessageBus.Host;

public readonly struct ProcessMessageResult(ProcessResult result, Exception exception, AbstractConsumerSettings consumerSettings, object response)
{
    public Exception Exception { get; init; } = exception;
    public AbstractConsumerSettings ConsumerSettings { get; init; } = consumerSettings;
    public object Response { get; init; } = response;
    public ProcessResult Result { get; init; } = result;
}

public interface IMessageProcessor<in TMessage>
{
    IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings { get; }

    /// <summary>
    /// Processes the arrived message
    /// </summary>
    /// <returns>Null, if message processing was successful, otherwise the Exception</returns>
    Task<ProcessMessageResult> ProcessMessage(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default);
}