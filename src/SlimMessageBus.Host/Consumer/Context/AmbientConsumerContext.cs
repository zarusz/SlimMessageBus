namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Consumer;

// Consumers and their dependencies can outlive the dispatch that created them.
internal sealed class AmbientConsumerContext : IConsumerContext
{
    internal static readonly IConsumerContext Instance = new AmbientConsumerContext();

    private AmbientConsumerContext() { }

    public string Path => MessageScope.CurrentConsumerContext.Path;
    public IReadOnlyDictionary<string, object> Headers => MessageScope.CurrentConsumerContext.Headers;
    public CancellationToken CancellationToken => MessageScope.CurrentConsumerContext.CancellationToken;
    public IMessageBus Bus => MessageScope.CurrentConsumerContext.Bus;
    public IDictionary<string, object> Properties => MessageScope.CurrentConsumerContext.Properties;
    public object Consumer => MessageScope.CurrentConsumerContext.Consumer;
}
