namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SlimMessageBus.Host.Config;

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

        public override Task ProduceToTransport(object message, string path, byte[] messagePayload, IDictionary<string, object> messageHeaders, CancellationToken cancellationToken = default)
        {
            var messageType = message.GetType();
            OnProduced(messageType, path, message);

            if (messageType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequestMessage<>)))
            {
                var req = Serializer.Deserialize(messageType, messagePayload);

                var resp = OnReply(messageType, path, req);
                if (resp == null)
                {
                    return Task.CompletedTask;
                }

                messageHeaders.TryGetHeader(ReqRespMessageHeaders.RequestId, out string replyTo);

                var resposeHeaders = CreateHeaders();
                resposeHeaders.SetHeader(ReqRespMessageHeaders.RequestId, replyTo);

                var responsePayload = Serializer.Serialize(resp.GetType(), resp);
                return OnResponseArrived(responsePayload, replyTo, resposeHeaders);
            }

            return Task.CompletedTask;
        }

        public override DateTimeOffset CurrentTime => CurrentTimeProvider();

        #endregion

        public Func<DateTimeOffset> CurrentTimeProvider { get; set; } = () => DateTimeOffset.UtcNow;

        public void TriggerPendingRequestCleanup()
        {
            PendingRequestManager.CleanPendingRequests();
        }
    }
}
