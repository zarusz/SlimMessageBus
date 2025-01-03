namespace SlimMessageBus.Host;

public abstract class ResponseMessageProcessor;

/// <summary>
/// The <see cref="IMessageProcessor{TMessage}"/> implementation that processes the responses arriving to the bus.
/// </summary>
/// <typeparam name="TTransportMessage"></typeparam>
public class ResponseMessageProcessor<TTransportMessage> : ResponseMessageProcessor, IMessageProcessor<TTransportMessage>
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
            _logger.LogError(e, "Error occurred while consuming response message, {Message}", transportMessage);
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
            _logger.LogDebug("The response message for request id {RequestId} arriving on path {Path} will be disregarded. Either the request had already expired, had been cancelled or it was already handled (this response message is a duplicate).", requestId, path);
            // ToDo: add and API hook to these kind of situation
            return null;
        }

        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var tookTimespan = _currentTimeProvider.CurrentTime.Subtract(requestState.Created);
                _logger.LogDebug("Response arrived for {Request} on path {Path} (time: {RequestTime} ms)", requestState, path, tookTimespan);
            }

            if (responseHeaders.TryGetHeader(ReqRespMessageHeaders.Error, out string errorMessage))
            {
                // error response arrived

                var responseException = new RequestHandlerFaultedMessageBusException(errorMessage);
                _logger.LogDebug(responseException, "Response arrived for {Request} on path {Path} with error: {ResponseError}", requestState, path, responseException.Message);
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
                    _logger.LogDebug(e, "Could not deserialize the response message for {Request} arriving on path {Path}", requestState, path);
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
}
