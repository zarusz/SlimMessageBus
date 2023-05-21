namespace SlimMessageBus.Host;

public interface IMessageProcessor<in TMessage>
{
    IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings { get; }

    /// <summary>
    /// Processes the arrived message
    /// </summary>
    /// <returns>Null, if message processing was sucessful, otherwise the Exception</returns>
    Task<(Exception Exception, AbstractConsumerSettings ConsumerSettings, object Response, object Message)> ProcessMessage(TMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, CancellationToken cancellationToken, IServiceProvider currentServiceProvider = null);
}