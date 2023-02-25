namespace SlimMessageBus;

/// <summary>
/// Marker interface for request messages.
/// </summary>
/// <typeparam name="TResponse">The response message type associated with the request</typeparam>
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Marker interface for request messages.
/// </summary>
/// <typeparam name="TResponse">The response message type associated with the request</typeparam>
[Obsolete("Please use the IRequest<T> going forward. This will be removed in future releases.")]
public interface IRequestMessage<out TResponse> : IRequest<TResponse>
{
}

// <summary>
/// Marker interface for request messages with no response message expected.
/// </summary>
/// <typeparam name="TResponse">The response message type associated with the request</typeparam>
public interface IRequest
{
}