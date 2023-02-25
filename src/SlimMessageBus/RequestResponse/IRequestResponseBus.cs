namespace SlimMessageBus;

/// <summary>
/// Bus to work with request-response messages.
/// </summary>
public interface IRequestResponseBus
{
    /// <summary>
    /// Sends a request message.
    /// </summary>
    /// <typeparam name="TResponse">The response message type</typeparam>
    /// <param name="request">The request message</param>
    /// <param name="path">Name of the topic (or queue) to send the request to. When null the default topic for request message type (or global default) will be used.</param>
    /// <param name="headers">The headers to add to the message. When null no additional headers will be added.</param>
    /// <param name="timeout">The timespan after which the Send request will be cancelled if no response arrives.</param>
    /// <param name="cancellationToken">Cancellation token to notify if the client no longer is interested in the response.</param>
    /// <returns>Task that represents the pending request.</returns>
    /// <exception cref="SendMessageBusException">When sending of the message failed</exception>
    /// <exception cref="ProducerMessageBusException">When sending of the message failed</exception>
    /// <exception cref="OperationCanceledException">When the request timeout or the request was cancelled (via CancellationToken)</exception>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request message which has no response. The call will be awaited until the request is processed. In case of an exception on the handler side, the exception it will be bubbled up to the sender.
    /// </summary>
    /// <param name="request">The request message</param>
    /// <param name="path">Name of the topic (or queue) to send the request to. When null the default topic (or queue) for request message type (or global default) will be used.</param>
    /// <param name="headers">The headers to add to the message. When null no additional headers will be added.</param>
    /// <param name="timeout">The timespan after which the Send request will be cancelled if no response arrives.</param>
    /// <param name="cancellationToken">Cancellation token to notify if the client no longer is interested in the response.</param>
    /// <returns>Task that represents the pending request.</returns>
    /// <exception cref="SendMessageBusException">When sending of the message failed</exception>
    /// <exception cref="ProducerMessageBusException">When sending of the message failed</exception>
    /// <exception cref="OperationCanceledException">When the request timeout or the request was cancelled (via CancellationToken)</exception>
    Task Send(IRequest request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request message.
    /// </summary>
    /// <typeparam name="TResponse">The response message type</typeparam>
    /// <typeparam name="TRequest">The request message type</typeparam>
    /// <param name="request">The request message</param>
    /// <param name="path">Name of the topic (or queue) to send the request to. When null the default topic (or queue) for request message type (or global default) will be used.</param>
    /// <param name="headers">The headers to add to the message. When null no additional headers will be added.</param>
    /// <param name="timeout">The timespan after which the Send request will be cancelled if no response arrives.</param>
    /// <param name="cancellationToken">Cancellation token to notify if the client no longer is interested in the response.</param>
    /// <returns>Task that represents the pending request.</returns>
    /// <exception cref="SendMessageBusException">When sending of the message failed</exception>
    /// <exception cref="ProducerMessageBusException">When sending of the message failed</exception>
    /// <exception cref="OperationCanceledException">When the request timeout or the request was cancelled (via CancellationToken)</exception>
    Task<TResponse> Send<TResponse, TRequest>(TRequest request, string path = null, IDictionary<string, object> headers = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}