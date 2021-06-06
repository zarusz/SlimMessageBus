namespace SlimMessageBus.Host.AzureEventHub
{
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;

    public class MessageContextInfo
    {
        public PartitionContext Context { get; }
        public EventData Message { get; }

        public MessageContextInfo(PartitionContext context, EventData message)
        {
            Context = context;
            Message = message;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"EventHubPath: {Context.EventHubPath}, ConsumerGroupName: {Context.ConsumerGroupName}, PartitionId: {Context.RuntimeInformation.PartitionId}, Offset: {Message.SystemProperties.Offset}";
        }

        #endregion
    }
}