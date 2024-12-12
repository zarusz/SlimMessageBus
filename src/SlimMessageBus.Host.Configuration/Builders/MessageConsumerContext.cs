namespace SlimMessageBus.Host;

public class MessageConsumerContext<T> : IConsumerContext<T>
{
    private readonly IConsumerContext _target;

    public MessageConsumerContext(IConsumerContext consumerContext, T message)
    {
        _target = consumerContext;
        Message = message;
    }

    public string Path => _target.Path;

    public IReadOnlyDictionary<string, object> Headers => _target.Headers;

    public CancellationToken CancellationToken => _target.CancellationToken;

    public IMessageBus Bus => _target.Bus;

    public IDictionary<string, object> Properties => _target.Properties;

    public object Consumer => _target.Consumer;

    public T Message { get; }
}
