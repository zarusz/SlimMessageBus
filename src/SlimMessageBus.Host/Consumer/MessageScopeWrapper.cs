namespace SlimMessageBus.Host.Consumer;

public sealed class MessageScopeWrapper : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _messageScope;
    private IDisposable _messageScopeDisposable;

    public IServiceProvider ServiceProvider => _messageScope;

    public MessageScopeWrapper(ILogger logger, IServiceProvider serviceProvider, bool createMessageScope, object message)
    {
        _logger = logger;
        _messageScope = serviceProvider;

        if (createMessageScope)
        {
            _logger.LogDebug("Creating message scope for {Message} of type {MessageType}", message, message.GetType());
            var ms = serviceProvider.CreateScope();
            _messageScope = ms.ServiceProvider;
            _messageScopeDisposable = ms;

            // Set the current scope only if one did not exist before
            MessageScope.Current = _messageScope;
        }
    }

    public ValueTask DisposeAsync()
    {
        // Note: We need to clear the MessageScope.Current in the same async context (without the async call as the ExecutionContext does not populate up the async chain call)
        // More on this: https://stackoverflow.com/a/56299915
        if (_messageScopeDisposable != null)
        {
            // Clear current scope only if one was started as part of this consumption
            MessageScope.Current = null;
        }

        // Suppress finalization.
        GC.SuppressFinalize(this);

        return DisposeAsyncCore();
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_messageScopeDisposable is not null)
        {
            if (_messageScopeDisposable is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                _messageScopeDisposable.Dispose();
            }
            _messageScopeDisposable = null;
        }
    }
}
