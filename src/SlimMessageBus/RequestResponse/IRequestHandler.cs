namespace SlimMessageBus;

/// <summary>
/// Handler for request messages in the request-response communication.
/// </summary>
/// <typeparam name="TRequest">The request message type</typeparam>
/// <typeparam name="TResponse">The response message type</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
{
    /// <summary>
    /// Handles the incoming request message.
    /// </summary>
    /// <param name="request">The request message</param>
    /// <returns></returns>
    Task<TResponse> OnHandle(TRequest request);
}

/// <summary>
/// Handler for request messages in the request-response communication where response is void.
/// </summary>
/// <typeparam name="TRequest">The request message type</typeparam>
public interface IRequestHandler<in TRequest>
{
    /// <summary>
    /// Handles the incoming request message.
    /// </summary>
    /// <param name="request">The request message</param>
    /// <returns></returns>
    Task OnHandle(TRequest request);
}