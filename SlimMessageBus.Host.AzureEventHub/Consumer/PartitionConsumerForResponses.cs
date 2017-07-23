using System;
using Common.Logging;
using Microsoft.ServiceBus.Messaging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    /// <summary>
    /// <see cref="PartitionConsumer"/> implementation meant for processing responses returning back in the request-response flows.
    /// </summary>
    public class PartitionConsumerForResponses : PartitionConsumer
    {
        private static readonly ILog Log = LogManager.GetLogger<PartitionConsumerForResponses>();

        private readonly RequestResponseSettings _requestResponseSettings;
        private readonly ICheckpointTrigger _checkpointTrigger;

        public PartitionConsumerForResponses(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings) 
            : base(messageBus)
        {
            _requestResponseSettings = requestResponseSettings;
            _checkpointTrigger = new CheckpointTrigger(requestResponseSettings);
        }

        #region Overrides of EventProcessor

        protected override bool OnSubmit(EventData message, PartitionContext context)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Message submitted: {0}", new MessageContextInfo(context, message));
            }
            try
            {
                MessageBus.OnResponseArrived(message.GetBytes(), _requestResponseSettings.Topic).Wait();
            }
            catch (Exception e)
            {
                if (Log.IsErrorEnabled)
                {
                    Log.ErrorFormat("Error occured while consuming response message, {0}", e, new MessageContextInfo(context, message));
                }

                // We can only continue and process all messages in the lease    

                if (_requestResponseSettings.OnResponseMessageFault != null)
                {
                    // Call the hook
                    Log.DebugFormat("Executing the attached hook from {0}", nameof(_requestResponseSettings.OnResponseMessageFault));
                    _requestResponseSettings.OnResponseMessageFault(_requestResponseSettings, message, e);
                }
            }
            return _checkpointTrigger.Increment();
        }

        protected override bool OnCommit(out EventData lastGoodMessage)
        {
            Log.Debug("Commiting...");
            _checkpointTrigger.Reset();
            lastGoodMessage = null;
            return true;
        }

        #endregion
    }
}