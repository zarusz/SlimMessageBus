namespace SlimMessageBus.Host.Test;

public class MessageBusTested : MessageBusBase
{
    internal int _startedCount;
    internal int _stoppedCount;

    public MessageBusTested(MessageBusSettings settings)
        : base(settings)
    {
        // by default no responses will arrive
        OnReply = (type, payload, req) => null;

        OnBuildProvider();
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

    protected override async Task<(IReadOnlyCollection<T> Dispatched, Exception Exception)> ProduceToTransport<T>(IReadOnlyCollection<T> envelopes, string path, IMessageBusTarget targetBus, CancellationToken cancellationToken = default)
    {
        await EnsureInitFinished();

        var dispatched = new List<T>(envelopes.Count);
        try
        {
            foreach (var envelope in envelopes)
            {
                var messageType = envelope.Message.GetType();

                OnProduced(messageType, path, envelope.Message);

                if (messageType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequest<>)))
                {
                    var messagePayload = Serializer.Serialize(envelope.MessageType, envelope.Message);
                    var req = Serializer.Deserialize(messageType, messagePayload);

                    var resp = OnReply(messageType, path, req);
                    if (resp == null)
                    {
                        continue;
                    }

                    envelope.Headers.TryGetHeader(ReqRespMessageHeaders.ReplyTo, out string replyTo);
                    envelope.Headers.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId);

                    var responseHeaders = CreateHeaders();
                    responseHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);

                    var responsePayload = Serializer.Serialize(resp.GetType(), resp);
                    await OnResponseArrived(responsePayload, replyTo, (IReadOnlyDictionary<string, object>)responseHeaders);
                }
            }
        }
        catch (Exception ex)
        {
            return (dispatched, ex);
        }

        return (dispatched, null);
    }

    public override DateTimeOffset CurrentTime => CurrentTimeProvider();

    #endregion

    public Func<DateTimeOffset> CurrentTimeProvider { get; set; } = () => DateTimeOffset.UtcNow;

    public void TriggerPendingRequestCleanup()
    {
        PendingRequestManager.CleanPendingRequests();
    }
}
