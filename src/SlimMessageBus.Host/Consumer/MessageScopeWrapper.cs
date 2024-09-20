namespace SlimMessageBus.Host.Consumer;

/// <summary>
/// Used by consumers to wrap the message processing in a message scope (MSDI).
/// The <see cref="MessageScope.Current"/> is being adjusted as part of this wrapper.
/// </summary>
public sealed class MessageScopeWrapper : IMessageScope
{
    private readonly IServiceProvider _messageScope;
    private readonly IServiceProvider _prevMessageScope;
    private IDisposable _messageScopeDisposable;

    public IServiceProvider ServiceProvider => _messageScope;

    public MessageScopeWrapper(IServiceProvider serviceProvider, bool createMessageScope)
    {
        _messageScope = serviceProvider;

        if (createMessageScope)
        {
            var ms = serviceProvider.CreateScope();
            _messageScope = ms.ServiceProvider;
            _messageScopeDisposable = ms;
        }

        // Set the current scope only if one did not exist before or changed 
        _prevMessageScope = MessageScope.Current;
        if (!ReferenceEquals(_prevMessageScope, _messageScope))
        {
            MessageScope.Current = _messageScope;
        }
    }

    public ValueTask DisposeAsync()
    {
        // Note: We need to clear the MessageScope.Current in the same async context (without the async call as the ExecutionContext does not populate up the async chain call)
        // More on this: https://stackoverflow.com/a/56299915
        if (!ReferenceEquals(_prevMessageScope, _messageScope))
        {
            // Clear current scope only if one was started as part of this consumption
            MessageScope.Current = _prevMessageScope;
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
