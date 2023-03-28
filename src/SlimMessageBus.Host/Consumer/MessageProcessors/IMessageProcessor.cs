namespace SlimMessageBus.Host;

public interface IMessageProcessor<TMessage> : IAsyncDisposable
{
    IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings { get; }

    /// <summary>
    /// Processes the arrived message
    /// </summary>
    /// <param name="transportMessage"></param>
    /// <returns>Null, if message processing was sucessful, otherwise the Exception</returns>
    Task<(Exception Exception, AbstractConsumerSettings ConsumerSettings, object Response)> ProcessMessage(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken);
}