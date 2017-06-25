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

        private TopicConsumerInstances<EventData> _instances;

        public EventProcessorForConsumers(EventHubConsumer consumer, ConsumerSettings consumerSettings)
            : base(consumer)
        {
            _instances = new TopicConsumerInstances<EventData>(consumerSettings, consumer.MessageBus, e => e.GetBytes());
        }

        #region Overrides of EventProcessor

        public override void Dispose()
        {
            if (_instances != null)
            {
                _instances.DisposeSilently("TopicConsumerInstances", Log);
                _instances = null;
            }
        }

        protected override Task<EventData> OnSubmit(EventData message)
        {
            return Task.FromResult(_instances.Submit(message));
        }

        protected override Task<EventData> OnCommit(EventData lastMessage)
        {
            return Task.FromResult(_instances.Commit(lastMessage));
        }

        #endregion
    }
}