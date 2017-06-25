using System;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    /// <summary>
    /// <see cref="EventProcessor"/> implementation meant for processing responses returning back in the request-response flows.
    /// </summary>
    public class EventProcessorForResponses : EventProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger<EventProcessorForResponses>();

        private readonly RequestResponseSettings _requestResponseSettings;

        public EventProcessorForResponses(EventHubConsumer consumer, RequestResponseSettings requestResponseSettings) 
            : base(consumer)
        {
            _requestResponseSettings = requestResponseSettings;
        }

        #region Overrides of EventProcessor

        public override void Dispose()
        {            
        }

        protected override async Task OnSubmit(EventData message)
        {
            try
            {
                await Consumer.MessageBus.OnResponseArrived(message.GetBytes(), _requestResponseSettings.Topic);
            }
            catch (Exception e)
            {
                // ToDo: add hook to capture these situations
                Log.ErrorFormat("Error occured while consuming response message, Offset: {0}, Topic: {1}, Group: {2}", e, message.Offset, _requestResponseSettings.Topic, _requestResponseSettings.Group);
                // for now continue until end all messages in lease are processed
            }
        }

        protected override Task<EventData> OnCommit(EventData lastMessage)
        {
            return Task.FromResult(lastMessage); 
        }

        #endregion
    }
}