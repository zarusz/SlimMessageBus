namespace SlimMessageBus.Host.Consumer;

public static class MessageScope
{
    private static readonly AsyncLocal<ScopeState> _current = new();

    public static IServiceProvider Current
    {
        get => _current.Value.ServiceProvider;
        // Keep the dispatch frame shared so disposal clears the context even after a provider override.
        set => _current.Value = new ScopeState(_current.Value.Frame, value, hasProviderOverride: true);
    }

    internal static ConsumerContext CurrentConsumerContext => _current.Value.Frame?.Context
        ?? throw new InvalidOperationException("The consumer context is only available while a message is being processed.");

    internal static bool IsCurrentContext(IConsumerContext context) => ReferenceEquals(_current.Value.Frame?.Context, context);

    internal static IDisposable BeginDispatch(IMessageScope messageScope, ConsumerContext context)
    {
        var current = _current.Value;
        if (messageScope is MessageScopeWrapper wrapper && ReferenceEquals(wrapper.Frame, current.Frame)
            && !current.HasProviderOverride && current.Frame.Context is null)
        {
            // The wrapper owns the frame and clears the context before disposing the DI scope.
            current.Frame.Context = context;
            return null;
        }

        return BeginDispatch(messageScope.ServiceProvider, context);
    }

    internal static DispatchFrame BeginDispatch(IServiceProvider serviceProvider, ConsumerContext context = null)
    {
        var frame = new DispatchFrame(serviceProvider, context);
        _current.Value = new ScopeState(frame);
        return frame;
    }

    private readonly struct ScopeState(DispatchFrame frame, IServiceProvider providerOverride = null, bool hasProviderOverride = false)
    {
        public DispatchFrame Frame { get; } = frame;
        public bool HasProviderOverride { get; } = hasProviderOverride;
        public IServiceProvider ServiceProvider => HasProviderOverride ? providerOverride : Frame?.ServiceProvider;
    }

    internal sealed class DispatchFrame : IDisposable
    {
        private ScopeState _previous;

        public ConsumerContext Context { get; set; }
        public IServiceProvider ServiceProvider { get; private set; }

        internal DispatchFrame(IServiceProvider serviceProvider, ConsumerContext context)
        {
            ServiceProvider = serviceProvider;
            Context = context;
            _previous = _current.Value;
        }

        public void Dispose()
        {
            // Clear the context in any execution context captured during this dispatch.
            Context = null;
            ServiceProvider = null;
            _current.Value = _previous;
            _previous = default;
        }
    }
}
