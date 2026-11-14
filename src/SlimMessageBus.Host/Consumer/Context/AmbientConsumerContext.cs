namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Consumer;

// Constructor-injected dependencies can outlive a dispatch when the caller's DI scope is reused.
internal sealed class AmbientConsumerContext : IConsumerContext
{
    public string Path => MessageScope.CurrentConsumerContext.Path;
    public IReadOnlyDictionary<string, object> Headers => MessageScope.CurrentConsumerContext.Headers;
    public CancellationToken CancellationToken => MessageScope.CurrentConsumerContext.CancellationToken;
    public IMessageBus Bus => MessageScope.CurrentConsumerContext.Bus;
    public IDictionary<string, object> Properties => MessageScope.CurrentConsumerContext.Properties;
    public object Consumer => MessageScope.CurrentConsumerContext.Consumer;
}
