namespace SlimMessageBus.Host.Kafka;

/// <summary>
/// Context object passed to <see cref="IKafkaLoopFailureInterceptor"/>.
/// </summary>
public class KafkaLoopFailureContext(string group, Exception exception)
{
    public string Group { get; } = group ?? throw new ArgumentNullException(nameof(group));
    public Exception Exception { get; } = exception ?? throw new ArgumentNullException(nameof(exception));
}