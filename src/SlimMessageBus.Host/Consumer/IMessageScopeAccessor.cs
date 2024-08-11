namespace SlimMessageBus.Host.Consumer;

/// <summary>
/// Allows to get ahold of the <see cref="IServiceProvider"/> for the current message scope.
/// </summary>
public interface IMessageScopeAccessor
{
    /// <summary>
    /// If the running code is within a message scope of a consumer, this property will return the <see cref="IServiceProvider"/> for the current message scope.
    /// Otherwise it will return null.
    /// </summary>
    IServiceProvider Current { get; }
}