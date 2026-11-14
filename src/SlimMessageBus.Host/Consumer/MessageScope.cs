namespace SlimMessageBus.Host.Consumer;

public static class MessageScope
{
    private static readonly AsyncLocal<(IServiceProvider ServiceProvider, ConsumerContextScope ConsumerScope)> _current = new();
    private static readonly IConsumerContext AmbientContext = new AmbientConsumerContext();

    public static IServiceProvider Current
    {
        get => _current.Value.ServiceProvider;
        set => _current.Value = (value, _current.Value.ConsumerScope);
    }

    private static ConsumerContextScope CurrentConsumerScope
    {
        get => _current.Value.ConsumerScope;
        set => _current.Value = (_current.Value.ServiceProvider, value);
    }

    internal static ConsumerContext CurrentConsumerContext => CurrentConsumerScope?.Context
        ?? throw new InvalidOperationException("The consumer context is only available while a message is being processed.");

    // In a dedicated message scope, preserve the identity of the context passed to the consumer.
    internal static IConsumerContext ResolveConsumerContext(IServiceProvider serviceProvider) =>
        CurrentConsumerScope is { Context: { } context } scope && ReferenceEquals(scope.OwnedServiceProvider, serviceProvider)
            ? context
            : AmbientContext;

    internal static IDisposable BeginConsumerDispatch(IServiceProvider ownedServiceProvider)
        => new ConsumerContextScope(ownedServiceProvider);

    internal static void SetConsumerContext(ConsumerContext context) => CurrentConsumerScope.Context = context;

    private sealed class ConsumerContextScope : IDisposable
    {
        private ConsumerContextScope _previous = CurrentConsumerScope;

        public ConsumerContext Context { get; set; }
        public IServiceProvider OwnedServiceProvider { get; private set; }

        public ConsumerContextScope(IServiceProvider ownedServiceProvider)
        {
            OwnedServiceProvider = ownedServiceProvider;
            CurrentConsumerScope = this;
        }

        public void Dispose()
        {
            // Clear the context in any execution context captured during this dispatch.
            Context = null;
            OwnedServiceProvider = null;
            CurrentConsumerScope = _previous;
            _previous = null;
        }
    }
}
