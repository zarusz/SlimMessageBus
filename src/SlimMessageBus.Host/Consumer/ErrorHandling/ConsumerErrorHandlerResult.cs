namespace SlimMessageBus.Host;

public record ConsumerErrorHandlerResult
{
    private static readonly object NoResponse = new();

    public bool Handled { get; private set; }
    public object Response { get; private set; }
    public bool HasResponse => !ReferenceEquals(Response, NoResponse);

    /// <summary>
    /// The error handler was not able to handle the exception.
    /// </summary>
    public static readonly ConsumerErrorHandlerResult Failure = new() { Handled = false, Response = NoResponse };
    /// <summary>
    /// The error handler was able to handle the exception.
    /// </summary>
    public static readonly ConsumerErrorHandlerResult Success = new() { Handled = true, Response = NoResponse };

    /// <summary>
    /// The error handler was able to handle the exception, and has a fallback response for the <see cref="IRequestHandler{TRequest}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/>.
    /// </summary>
    public static ConsumerErrorHandlerResult SuccessWithResponse(object response) => new() { Handled = true, Response = response };
}