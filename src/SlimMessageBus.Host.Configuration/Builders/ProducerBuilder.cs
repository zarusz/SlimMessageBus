namespace SlimMessageBus.Host;

public class ProducerBuilder<T>
{
    public ProducerSettings Settings { get; }

    public Type MessageType => Settings.MessageType;

    public ProducerBuilder(ProducerSettings settings)
        : this(settings, typeof(T))
    {
    }

    public ProducerBuilder(ProducerSettings settings, Type messageType)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Settings.MessageType = messageType;
    }

    public ProducerBuilder<T> DefaultPath(string path)
    {
        Settings.DefaultPath = path ?? throw new ArgumentNullException(nameof(path));
        return this;
    }

    public ProducerBuilder<T> DefaultTopic(string topic) => DefaultPath(topic);

    public ProducerBuilder<T> DefaultTimeout(TimeSpan timeout)
    {
        Settings.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Hook called whenver message is being produced. Can be used to add (or mutate) message headers.
    /// </summary>
    public ProducerBuilder<T> WithHeaderModifier(MessageHeaderModifier<T> headerModifier)
    {
        if (headerModifier == null) throw new ArgumentNullException(nameof(headerModifier));

        Settings.HeaderModifier = (headers, message) => headerModifier(headers, (T)message);
        return this;
    }
}