namespace SlimMessageBus.Host;

public abstract class ResponseMessageProcessor;

/// <summary>
/// The <see cref="IMessageProcessor{TMessage}"/> implementation that processes the responses arriving to the bus.
/// </summary>
/// <typeparam name="TTransportMessage"></typeparam>
public partial class ResponseMessageProcessor<TTransportMessage> : ResponseMessageProcessor, IMessageProcessor<TTransportMessage>
{
    private readonly ILogger<ResponseMessageProcessor> _logger;
    private readonly RequestResponseSettings _requestResponseSettings;
    private readonly IReadOnlyCollection<AbstractConsumerSettings> _consumerSettings;
    private readonly MessageProvider<TTransportMessage> _messageProvider;
    private readonly IPendingRequestStore _pendingRequestStore;
    private readonly ICurrentTimeProvider _currentTimeProvider;

    public ResponseMessageProcessor(ILoggerFactory loggerFactory,
                                    RequestResponseSettings requestResponseSettings,
                                    MessageProvider<TTransportMessage> messageProvider,
                                    IPendingRequestStore pendingRequestStore,
                                    ICurrentTimeProvider currentTimeProvider)
    {
        if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));

        _logger = loggerFactory.CreateLogger<ResponseMessageProcessor>();
        _requestResponseSettings = requestResponseSettings ?? throw new ArgumentNullException(nameof(requestResponseSettings));
        _consumerSettings = [_requestResponseSettings];
        _messageProvider = messageProvider ?? throw new ArgumentNullException(nameof(messageProvider));
        _pendingRequestStore = pendingRequestStore;
        _currentTimeProvider = currentTimeProvider;
    }

    public IReadOnlyCollection<AbstractConsumerSettings> ConsumerSettings => _consumerSettings;

    public Task<ProcessMessageResult> ProcessMessage(TTransportMessage transportMessage, IReadOnlyDictionary<string, object> messageHeaders, IDictionary<string, object> consumerContextProperties = null, IServiceProvider currentServiceProvider = null, CancellationToken cancellationToken = default)
    {
        Exception ex;
        try
        {
            ex = OnResponseArrived(transportMessage, _requestResponseSettings.Path, messageHeaders);
        }
        catch (Exception e)
        {
            LogErrorConsumingResponse(transportMessage, e);
            // We can only continue and process all messages in the lease    
            ex = e;
        }

        var result = ex == null ? ProcessResult.Success : ProcessResult.Failure;
        return Task.FromResult(new ProcessMessageResult(result, ex, _requestResponseSettings, null));
    }

    /// <summary>
    /// Should be invoked by the concrete bus implementation whenever there is a message arrived on the reply to topic.
    /// </summary>
    /// <param name="transportMessage">The response message</param>
    /// <param name="path"></param>
    /// <param name="responseHeaders">The response message headers</param>
    /// <returns></returns>
    private Exception OnResponseArrived(TTransportMessage transportMessage, string path, IReadOnlyDictionary<string, object> responseHeaders)
    {
        if (!responseHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId))
        {
            return new ConsumerMessageBusException($"The response message arriving on path {path} did not have the {ReqRespMessageHeaders.RequestId} header. Unable to math the response with the request. This likely indicates a misconfiguration.");
        }

        var requestState = _pendingRequestStore.GetById(requestId);
        if (requestState == null)
        {
            LogResponseWillBeDiscarded(path, requestId);
            // ToDo: add and API hook to these kind of situation
            return null;
        }

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var requestTime = _currentTimeProvider.CurrentTime.Subtract(requestState.Created);
                LogResponseArrived(path, requestState, requestTime);
            }

            if (responseHeaders.TryGetHeader(ReqRespMessageHeaders.Error, out string errorMessage))
            {
                // error response arrived

                var responseException = new RequestHandlerFaultedMessageBusException(errorMessage);
                LogResponseArrivedWithError(path, requestState, responseException, responseException.Message);
                requestState.TaskCompletionSource.TrySetException(responseException);
            }
            else
            {
                // response arrived
                try
                {
                    // deserialize the response message
                    var response = transportMessage != null
                        ? _messageProvider(requestState.ResponseType, transportMessage)
                        : null;

                    // resolve the response
                    requestState.TaskCompletionSource.TrySetResult(response);
                }
                catch (Exception e)
                {
                    LogResponseCouldNotDeserialize(path, requestState, e);
                    requestState.TaskCompletionSource.TrySetException(e);
                }
            }
        }
        finally
        {
            // remove the request from the queue
            _pendingRequestStore.Remove(requestId);
        }

        return null;
    }

    #region Logging

    [LoggerMessage(
       EventId = 0,
       Level = LogLevel.Error,
       Message = "Error occurred while consuming response message, {Message}")]
    private partial void LogErrorConsumingResponse(TTransportMessage message, Exception e);

    [LoggerMessage(
       EventId = 1,
       Level = LogLevel.Debug,
       Message = "The response message for request id {RequestId} arriving on path {Path} will be disregarded. Either the request had already expired, had been cancelled or it was already handled (this response message is a duplicate).")]
    private partial void LogResponseWillBeDiscarded(string path, string requestId);

    [LoggerMessage(
       EventId = 2,
       Level = LogLevel.Debug,
       Message = "Response arrived for {RequestState} on path {Path} (time: {RequestTime} ms)")]
    private partial void LogResponseArrived(string path, PendingRequestState requestState, TimeSpan requestTime);

    [LoggerMessage(
       EventId = 3,
       Level = LogLevel.Debug,
       Message = "Response arrived for {RequestState} on path {Path} with error: {ResponseError}")]
    private partial void LogResponseArrivedWithError(string path, PendingRequestState requestState, Exception e, string responseError);

    [LoggerMessage(
       EventId = 4,
       Level = LogLevel.Debug,
       Message = "Could not deserialize the response message for {RequestState} arriving on path {Path}")]
    private partial void LogResponseCouldNotDeserialize(string path, PendingRequestState requestState, Exception e);

    #endregion
}

#if NETSTANDARD2_0

public partial class ResponseMessageProcessor<TTransportMessage>
{
    private partial void LogErrorConsumingResponse(TTransportMessage message, Exception e)
        => _logger.LogError(e, "Error occurred while consuming response message, {Message}", message);

    private partial void LogResponseWillBeDiscarded(string path, string requestId)
        => _logger.LogDebug("The response message for request id {RequestId} arriving on path {Path} will be disregarded. Either the request had already expired, had been cancelled or it was already handled (this response message is a duplicate).", requestId, path);

    private partial void LogResponseArrived(string path, PendingRequestState requestState, TimeSpan requestTime)
        => _logger.LogDebug("Response arrived for {RequestState} on path {Path} (time: {RequestTime} ms)", requestState, path, requestTime);

    private partial void LogResponseArrivedWithError(string path, PendingRequestState requestState, Exception e, string responseError)
        => _logger.LogDebug(e, "Response arrived for {RequestState} on path {Path} with error: {ResponseError}", requestState, path, responseError);

    private partial void LogResponseCouldNotDeserialize(string path, PendingRequestState requestState, Exception e)
        => _logger.LogDebug(e, "Could not deserialize the response message for {RequestState} arriving on path {Path}", requestState, path);
}

#endif