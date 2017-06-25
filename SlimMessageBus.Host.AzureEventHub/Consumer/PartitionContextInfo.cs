using Microsoft.ServiceBus.Messaging;

namespace SlimMessageBus.Host.AzureEventHub
{
    public class PartitionContextInfo
    {
        public readonly PartitionContext Context;

        public PartitionContextInfo(PartitionContext context)
        {
            Context = context;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"EventHubPath: {Context.EventHubPath}, ConsumerGroupName: {Context.ConsumerGroupName}, Lease.PartitionId: {Context.Lease.PartitionId}, Lease.Offset: {Context.Lease.Offset}";
        }

        #endregion
    }
}