namespace SlimMessageBus.Host.Config;

public class RequestResponseBuilder
{
    public RequestResponseSettings Settings { get; }

    public RequestResponseBuilder(RequestResponseSettings settings)
    {
        Settings = settings;
    }

    public RequestResponseBuilder DefaultTimeout(TimeSpan timeout)
    {
        Settings.Timeout = timeout;
        return this;
    }

    public RequestResponseBuilder ReplyToTopic(string topic)
    {
        Settings.Path = topic;
        return this;
    }
}