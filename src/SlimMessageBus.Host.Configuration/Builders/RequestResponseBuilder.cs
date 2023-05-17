namespace SlimMessageBus.Host;

public class RequestResponseBuilder
{
    public RequestResponseSettings Settings { get; }

    public RequestResponseBuilder(RequestResponseSettings settings)
    {
        Settings = settings;
    }

    /// <summary>
    /// Sets the response await timeout, after which the send operation is cancelled (and the request message is discarded at the handler side).
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public RequestResponseBuilder DefaultTimeout(TimeSpan timeout)
    {
        Settings.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Which path should the handler send the responses to.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public RequestResponseBuilder ReplyToPath(string path)
    {
        Settings.Path = path;
        return this;
    }

    /// <summary>
    /// Which topic should the handler send the responses to.
    /// </summary>
    /// <remarks>This is an alias to <see cref="ReplyToPath(string)"/></remarks>
    /// <param name="topic"></param>
    /// <returns></returns>
    public RequestResponseBuilder ReplyToTopic(string topic) => ReplyToPath(topic);
}