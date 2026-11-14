namespace SlimMessageBus.Host;

internal sealed class ConsumerContextScope : IDisposable
{
    private static readonly AsyncLocal<ConsumerContextScope> AmbientScope = new();
    private static readonly IConsumerContext AmbientContext = new AmbientConsumerContext();
    private readonly ConsumerContextScope _previous = AmbientScope.Value;
    private IServiceProvider _ownedServiceProvider;

    public ConsumerContext Context { get; set; }

    public static ConsumerContext Current => AmbientScope.Value?.Context
        ?? throw new InvalidOperationException("The consumer context is only available while a message is being processed.");

    // In a dedicated message scope, preserve the identity of the context passed to the consumer.
    public static IConsumerContext Resolve(IServiceProvider serviceProvider) =>
        AmbientScope.Value is { Context: { } context } scope && ReferenceEquals(scope._ownedServiceProvider, serviceProvider)
            ? context
            : AmbientContext;

    public ConsumerContextScope(IServiceProvider ownedServiceProvider)
    {
        _ownedServiceProvider = ownedServiceProvider;
        AmbientScope.Value = this;
    }

    public void Dispose()
    {
        // Clear the context in any execution context captured during this dispatch.
        Context = null;
        _ownedServiceProvider = null;
        AmbientScope.Value = _previous;
    }
}
