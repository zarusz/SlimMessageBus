using System.Threading.Tasks;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    /// <summary>
    /// <see cref="EventProcessor"/> implementation meant for processing messages comming to consumers (<see cref="IConsumer{TMessage}"/>) in pub-sub or handlers (<see cref="IRequestHandler{TRequest,TResponse}"/>) in request-response flows.
    /// </summary>
    public class EventProcessorForConsumers : EventProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger<EventProcessorForConsumers>();

        private ConsumerInstancePool<EventData> _instancePool;
        private readonly MessageQueueWorker<EventData> _queueWorker; 

        public EventProcessorForConsumers(EventHubConsumer consumer, ConsumerSettings consumerSettings)
            : base(consumer)
        {
            _instancePool = new ConsumerInstancePool<EventData>(consumerSettings, consumer.MessageBus, e => e.GetBytes());
            _queueWorker = new MessageQueueWorker<EventData>(_instancePool);

            if (consumerSettings.Properties.ContainsKey(Consts.CheckpointCount))
                CheckpointCount = (int) consumerSettings.Properties[Consts.CheckpointCount];

            if (consumerSettings.Properties.ContainsKey(Consts.CheckpointDuration))
                CheckpointDuration = (int) consumerSettings.Properties[Consts.CheckpointDuration];
        }

        #region Overrides of EventProcessor

        public override void Dispose()
        {
            if (_instancePool != null)
            {
                _instancePool.DisposeSilently("TopicConsumerInstances", Log);
                _instancePool = null;
            }
        }

        protected override Task OnSubmit(EventData message)
        {
            _queueWorker.Submit(message);
            return Task.CompletedTask;
        }

        protected override Task<EventData> OnCommit(EventData lastMessage)
        {
            return Task.FromResult(_queueWorker.Commit(lastMessage));
        }

        #endregion
    }
}