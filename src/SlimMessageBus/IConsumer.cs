namespace SlimMessageBus;

/// <summary>
/// Consumer for messages of type <typeparam name="TMessage"></typeparam>.
/// </summary>
/// <typeparam name="TMessage"></typeparam>
public interface IConsumer<in TMessage>
{
    /// <summary>
    /// Invoked when a message arrives of type <typeparam name="TMessage"></typeparam>.
    /// </summary>
    /// <param name="message">The arriving message</param>
    /// <param name="path">Name of the topic (or queue) the message arrived on</param>
    /// <returns></returns>
    Task OnHandle(TMessage message, string path);
}