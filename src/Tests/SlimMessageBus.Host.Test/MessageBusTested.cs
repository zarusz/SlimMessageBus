namespace SlimMessageBus.Host.Test;

public class MessageBusTested : MessageBusBase
{
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

    protected override async Task<object> ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken = default)
    {
        var messageType = message.GetType();
        OnProduced(messageType, path, message);

        if (messageType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequest<>)))
        {
            var req = Serializer.Deserialize(messageType, messagePayload);

            var resp = OnReply(messageType, path, req);
            if (resp == null)
            {
                return null;
            }

            messageHeaders.TryGetHeader(ReqRespMessageHeaders.ReplyTo, out string replyTo);
            messageHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out string requestId);

            var resposeHeaders = CreateHeaders();
            resposeHeaders.SetHeader(ReqRespMessageHeaders.RequestId, requestId);

            var responsePayload = Serializer.Serialize(resp.GetType(), resp);
            await OnResponseArrived(responsePayload, replyTo, (IReadOnlyDictionary<string, object>)resposeHeaders);
        }

        return null;
    }

    public override DateTimeOffset CurrentTime => CurrentTimeProvider();

    #endregion

    public Func<DateTimeOffset> CurrentTimeProvider { get; set; } = () => DateTimeOffset.UtcNow;

    public void TriggerPendingRequestCleanup()
    {
        PendingRequestManager.CleanPendingRequests();
    }
}
