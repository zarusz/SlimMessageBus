using System.Reflection;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    /// <summary>
    /// <see cref="PartitionConsumer"/> implementation meant for processing messages coming to consumers (<see cref="IConsumer{TMessage}"/>) in pub-sub or handlers (<see cref="IRequestHandler{TRequest,TResponse}"/>) in request-response flows.
    /// </summary>
    public class PartitionConsumerForConsumers : PartitionConsumer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ConsumerInstancePoolMessageProcessor<EventData> _instancePool;
        private readonly MessageQueueWorker<EventData> _queueWorker; 

        public PartitionConsumerForConsumers(EventHubMessageBus messageBus, ConsumerSettings consumerSettings)
            : base(messageBus)
        {
            _instancePool = new ConsumerInstancePoolMessageProcessor<EventData>(consumerSettings, messageBus, e => e.Body.Array);
            _queueWorker = new MessageQueueWorker<EventData>(_instancePool, new CheckpointTrigger(consumerSettings));
        }

        #region Overrides of EventProcessor

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _instancePool.DisposeSilently(nameof(ConsumerInstancePoolMessageProcessor<EventData>), Log);
            }
            base.Dispose(disposing);
        }

        protected override bool OnSubmit(EventData message, PartitionContext context)
        {
            return _queueWorker.Submit(message);
        }

        protected override Task<MessageQueueResult<EventData>> OnCommit()
        {
            return _queueWorker.WaitAll();
        }

        #endregion
    }
}