using Microsoft.ServiceBus.Messaging;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class MessageContextInfo
    {
        public readonly PartitionContext Context;
        public readonly EventData Message;

        public MessageContextInfo(PartitionContext context, EventData message)
        {
            Context = context;
            Message = message;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"EventHubPath: {Context.EventHubPath}, ConsumerGroupName: {Context.ConsumerGroupName}, Lease.PartitionId: {Context.Lease.PartitionId}, Message.Offset: {Message.Offset}";
        }

        #endregion
    }
}