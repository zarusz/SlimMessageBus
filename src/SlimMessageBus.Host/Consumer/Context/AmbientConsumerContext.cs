namespace SlimMessageBus.Host;

// Constructor-injected dependencies can outlive a dispatch when the caller's DI scope is reused.
internal sealed class AmbientConsumerContext : IConsumerContext
{
    public string Path => ConsumerContextScope.Current.Path;
    public IReadOnlyDictionary<string, object> Headers => ConsumerContextScope.Current.Headers;
    public CancellationToken CancellationToken => ConsumerContextScope.Current.CancellationToken;
    public IMessageBus Bus => ConsumerContextScope.Current.Bus;
    public IDictionary<string, object> Properties => ConsumerContextScope.Current.Properties;
    public object Consumer => ConsumerContextScope.Current.Consumer;
}
