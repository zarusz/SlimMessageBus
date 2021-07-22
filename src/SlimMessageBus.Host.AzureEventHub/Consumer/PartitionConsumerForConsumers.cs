namespace SlimMessageBus.Host.AzureEventHub
{
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.EventHubs.Processor;
    using Microsoft.Extensions.Logging;
    using SlimMessageBus.Host.Config;

    /// <summary>
    /// <see cref="PartitionConsumer"/> implementation meant for processing messages coming to consumers (<see cref="IConsumer{TMessage}"/>) in pub-sub or handlers (<see cref="IRequestHandler{TRequest,TResponse}"/>) in request-response flows.
    /// </summary>
    public class PartitionConsumerForConsumers : PartitionConsumer
    {
        private readonly ILogger _logger;
        private readonly ConsumerInstancePoolMessageProcessor<EventData> _instancePool;
        private readonly MessageQueueWorker<EventData> _queueWorker;

        public PartitionConsumerForConsumers(EventHubMessageBus messageBus, ConsumerSettings consumerSettings)
            : base(messageBus)
        {
            _logger = messageBus.LoggerFactory.CreateLogger<PartitionConsumerForConsumers>();
            _instancePool = new ConsumerInstancePoolMessageProcessor<EventData>(consumerSettings, messageBus, e => new MessageWithHeaders(e.Body.Array, e.Properties));
            _queueWorker = new MessageQueueWorker<EventData>(_instancePool, new CheckpointTrigger(consumerSettings), messageBus.LoggerFactory);
        }

        #region Overrides of EventProcessor

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _instancePool.DisposeSilently(nameof(ConsumerInstancePoolMessageProcessor<EventData>), _logger);
            }
            base.Dispose(disposing);
        }

        protected override bool OnSubmit(EventData message, PartitionContext context) 
            => _queueWorker.Submit(message);

        protected override Task<MessageQueueResult<EventData>> OnCommit() 
            => _queueWorker.WaitAll();

        #endregion
    }
}