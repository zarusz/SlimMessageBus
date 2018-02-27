
using Microsoft.Azure.EventHubs.Processor;

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
            return $"EventHubPath: {Context.EventHubPath}, ConsumerGroupName: {Context.ConsumerGroupName}, PartitionId: {Context.PartitionId}, Offset: {Context.RuntimeInformation.LastEnqueuedOffset}";
        }

        #endregion
    }
}