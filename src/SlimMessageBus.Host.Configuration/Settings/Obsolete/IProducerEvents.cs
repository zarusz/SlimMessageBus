namespace SlimMessageBus.Host;

public interface IProducerEvents
{
    /// <summary>
    /// Called whenever a message is produced.
    /// </summary>
    [Obsolete("Please use the interceptors https://github.com/zarusz/SlimMessageBus/blob/master/docs/intro.md#interceptors")]
    Action<IMessageBus, ProducerSettings, object, string> OnMessageProduced { get; set; }
}