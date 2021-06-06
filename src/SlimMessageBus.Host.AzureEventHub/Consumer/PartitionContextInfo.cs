namespace SlimMessageBus.Host.AzureEventHub
{

    using Microsoft.Azure.EventHubs.Processor;

    public class PartitionContextInfo
    {
        public PartitionContext Context { get; }

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