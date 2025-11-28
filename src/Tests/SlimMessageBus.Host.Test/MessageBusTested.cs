namespace SlimMessageBus.Host.Test;

public class MessageBusTested : MessageBusBase
{
    internal int _startedCount;
    internal int _stoppedCount;

    public IMessageProcessor<object> RequestResponseMessageProcessor { get; private set; }

    /// <summary>
    /// When true, all messages (not just requests) will be serialized/deserialized through the serializer.
    /// This is useful for testing that the correct messageType is passed to the serializer.
    /// </summary>
    public bool SerializeAllMessages { get; set; }

    public MessageBusTested(MessageBusSettings settings, TimeProvider timeProvider)
        : base(settings)
    {
        // by default no responses will arrive
        OnReply = (type, payload, req) => null;

        TimeProvider = timeProvider;
        OnBuildProvider();
    }

    protected override async Task CreateConsumers()
    {
        await base.CreateConsumers();

        if (Settings.RequestResponse != null)
        {
            RequestResponseMessageProcessor = new ResponseMessageProcessor<object>(LoggerFactory, Settings.RequestResponse, (mt, h, m) => m, PendingRequestStore, TimeProvider);
            AddConsumer(new MessageBusTestedConsumer(NullLogger.Instance));
        }
    }

    public ProducerSettings Public_GetProducerSettings(Type messageType) => GetProducerSettings(messageType);

    public int PendingRequestsCount => PendingRequestStore.GetCount();

    public Func<Type, string, object, object> OnReply { get; set; }
    public Action<Type, string, object> OnProduced { get; set; }

    #region Overrides of MessageBusBase

    protected internal override Task OnStart()
    {
        Interlocked.Increment(ref _startedCount);
        return base.OnStart();
    }

    protected internal override Task OnStop()
    {
        Interlocked.Increment(ref _stoppedCount);
        return base.OnStop();
    }

    public override async Task ProduceToTransport(object message, Type messageType, string path, IDictionary<string, object> messageHeaders, IMessageBusTarget targetBus, CancellationToken cancellationToken)
    {
        OnProduced(messageType, path, message);

        var isRequest = messageType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequest<>));

        // Serialize all messages if SerializeAllMessages is enabled, or if it's a request
        if (SerializeAllMessages || isRequest)
        {
            var messageSerializer = SerializerProvider.GetSerializer(path);
            var messagePayload = messageSerializer.Serialize(messageType, messageHeaders, message, null);
            
            // Only deserialize and process response for requests
            if (isRequest)
            {
                var req = messageSerializer.Deserialize(messageType, messageHeaders.AsReadOnly(), messagePayload, null);

                var resp = OnReply(messageType, path, req);
                if (resp == null)
                {
                    return;
                }

                messageHeaders.TryGetHeader(ReqRespMessageHeaders.ReplyTo, out string replyTo);
                messageHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId);

                var responseHeaders = CreateHeaders() as Dictionary<string, object>;
                responseHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);

                await RequestResponseMessageProcessor.ProcessMessage(resp, responseHeaders, null, null, cancellationToken);
            }
        }
    }

    #endregion

    public void TriggerPendingRequestCleanup()
    {
        PendingRequestManager.CleanPendingRequests();
    }

    public class MessageBusTestedConsumer(ILogger logger) : AbstractConsumer(logger, [], "path", [])
    {
        internal protected override Task OnStart() => Task.CompletedTask;

        internal protected override Task OnStop() => Task.CompletedTask;
    }
}
