namespace SlimMessageBus.Host;

public abstract record ProcessResult
{
    private static readonly object _noResponse = new();

    protected ProcessResult(object response = null)
    {
        Response = response ?? _noResponse;
    }

    public object Response { get; }
    public bool HasResponse => !ReferenceEquals(Response, _noResponse);

    /// <summary>
    /// The message should be placed back into the queue.
    /// </summary>
    public static readonly ProcessResult Failure = new FailureState();

    /// <summary>
    /// Retry processing the message without placing it back in the queue.
    /// </summary>
    public static readonly ProcessResult Retry = new RetryState();

    /// <summary>
    /// Processing was canceled. The transport should leave the delivery available for redelivery when supported.
    /// </summary>
    public static readonly ProcessResult Canceled = new CanceledState();

    /// <summary>
    /// The message processor should evaluate the message as having been processed successfully.
    /// </summary>
    public static readonly ProcessResult Success = new SuccessState();

    /// <summary>
    /// The message processor should evaluate the message as having been processed successfully and use the specified fallback response for the <see cref="IRequestHandler{TRequest}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    public static ProcessResult SuccessWithResponse(object response) => new SuccessStateWithResponse(response);

    public record FailureState() : ProcessResult();
    public record RetryState() : ProcessResult();
    public record CanceledState() : ProcessResult();
    public record SuccessState(object Response = null) : ProcessResult(Response);
    public record SuccessStateWithResponse(object Response) : SuccessState(Response);
}
