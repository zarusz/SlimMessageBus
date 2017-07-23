using Common.Logging;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    /// <summary>
    /// <see cref="PartitionConsumer"/> implementation meant for processing messages comming to consumers (<see cref="IConsumer{TMessage}"/>) in pub-sub or handlers (<see cref="IRequestHandler{TRequest,TResponse}"/>) in request-response flows.
    /// </summary>
    public class PartitionConsumerForConsumers : PartitionConsumer
    {
        private static readonly ILog Log = LogManager.GetLogger<PartitionConsumerForConsumers>();

        private readonly ConsumerInstancePool<EventData> _instancePool;
        private readonly MessageQueueWorker<EventData> _queueWorker; 

        public PartitionConsumerForConsumers(EventHubMessageBus messageBus, ConsumerSettings consumerSettings)
            : base(messageBus)
        {
            _instancePool = new ConsumerInstancePool<EventData>(consumerSettings, messageBus, e => e.GetBytes());
            _queueWorker = new MessageQueueWorker<EventData>(_instancePool, new CheckpointTrigger(consumerSettings));
        }

        #region Overrides of EventProcessor

        public override void Dispose()
        {
            base.Dispose();
            _instancePool.DisposeSilently(nameof(ConsumerInstancePool<EventData>), Log);
        }

        protected override bool OnSubmit(EventData message, PartitionContext context)
        {
            return _queueWorker.Submit(message);
        }

        protected override bool OnCommit(out EventData lastGoodMessage)
        {
            return _queueWorker.Commit(out lastGoodMessage);
        }

        #endregion
    }
}