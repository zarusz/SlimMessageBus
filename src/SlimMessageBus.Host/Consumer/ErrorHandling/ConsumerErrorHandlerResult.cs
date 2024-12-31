namespace SlimMessageBus.Host;

public record ConsumerErrorHandlerResult
{
    private static readonly object _noResponse = new();

    private ConsumerErrorHandlerResult(ProcessResult result, object response = null)
    {
        Result = result;
        Response = response ?? _noResponse;
    }

    public ProcessResult Result { get; private set; }
    public object Response { get; private set; }
    public bool HasResponse => !ReferenceEquals(Response, _noResponse);

    /// <summary>
    /// the message should be abandoned and placed in the dead letter queue.
    /// </summary>
    /// <note>
    /// This feature is not supported by every transport.
    /// </note>
    public static readonly ConsumerErrorHandlerResult Abandon = new(ProcessResult.Abandon);

    /// <summary>
    /// The message should be placed back into the queue.
    /// </summary>
    public static readonly ConsumerErrorHandlerResult Failure = new(ProcessResult.Fail);

    /// <summary>
    /// The message processor should evaluate the message as having been processed successfully.
    /// </summary>
    public static readonly ConsumerErrorHandlerResult Success = new(ProcessResult.Success);

    /// <summary>
    /// The message processor should evaluate the message as having been processed successfully and use the specified fallback response for the <see cref="IRequestHandler{TRequest}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    public static ConsumerErrorHandlerResult SuccessWithResponse(object response) => new(ProcessResult.Success, response);

    /// <summary>
    /// Retry processing the message without placing it back in the queue.
    /// </summary>
    public static readonly ConsumerErrorHandlerResult Retry = new(ProcessResult.Retry);
}
