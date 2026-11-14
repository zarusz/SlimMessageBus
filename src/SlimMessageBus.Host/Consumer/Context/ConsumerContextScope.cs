namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Consumer;

internal sealed class ConsumerContextScope : IDisposable
{
    private static readonly IConsumerContext AmbientContext = new AmbientConsumerContext();
    private ConsumerContextScope _previous = MessageScope.CurrentConsumerScope;
    private IServiceProvider _ownedServiceProvider;

    public ConsumerContext Context { get; set; }

    public static ConsumerContext Current => MessageScope.CurrentConsumerScope?.Context
        ?? throw new InvalidOperationException("The consumer context is only available while a message is being processed.");

    // In a dedicated message scope, preserve the identity of the context passed to the consumer.
    public static IConsumerContext Resolve(IServiceProvider serviceProvider) =>
        MessageScope.CurrentConsumerScope is { Context: { } context } scope && ReferenceEquals(scope._ownedServiceProvider, serviceProvider)
            ? context
            : AmbientContext;

    public ConsumerContextScope(IServiceProvider ownedServiceProvider)
    {
        _ownedServiceProvider = ownedServiceProvider;
        MessageScope.CurrentConsumerScope = this;
    }

    public void Dispose()
    {
        // Clear the context in any execution context captured during this dispatch.
        Context = null;
        _ownedServiceProvider = null;
        MessageScope.CurrentConsumerScope = _previous;
        _previous = null;
    }
}
