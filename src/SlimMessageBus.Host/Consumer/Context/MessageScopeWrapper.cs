namespace SlimMessageBus.Host;

public class MessageScopeWrapper : IDependencyResolver, IDisposable
{
    private readonly ILogger _logger;
    private readonly IDependencyResolver _messageScope;
    private readonly bool _shouldDispose;

    public MessageScopeWrapper(ILogger logger, IDependencyResolver dependencyResolver, bool createMessageScope, object message)
    {
        _logger = logger;
        _messageScope = dependencyResolver;

        // Capture if an existing scope has already been started
        var existingScope = MessageScope.Current;
        if (existingScope != null)
        {
            _logger.LogDebug("Joining existing message scope for {Message} of type {MessageType}", message, message.GetType());
            _messageScope = existingScope;
        }
        else if (createMessageScope)
        {
            _logger.LogDebug("Creating message scope for {Message} of type {MessageType}", message, message.GetType());
            _messageScope = dependencyResolver.CreateScope();

            // Set the current scope only if one did not exist before
            MessageScope.Current = _messageScope;
        }

        _shouldDispose = existingScope == null && createMessageScope;
    }

    public IChildDependencyResolver CreateScope() => _messageScope.CreateScope();

    public object Resolve(Type type) => _messageScope.Resolve(type);

    public void Dispose()
    {
        if (_shouldDispose && _messageScope is IDisposable disposable)
        {
            // Clear current scope only if one was started as part of this consumption
            MessageScope.Current = null;

            disposable.Dispose();
        }
    }
}
