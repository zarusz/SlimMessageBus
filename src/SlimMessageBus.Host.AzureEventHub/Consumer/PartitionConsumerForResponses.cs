using System;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Extensions.Logging;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub
{
    /// <summary>
    /// <see cref="PartitionConsumer"/> implementation meant for processing responses returning back in the request-response flows.
    /// </summary>
    public class PartitionConsumerForResponses : PartitionConsumer
    {
        private readonly ILogger _logger;
        private readonly RequestResponseSettings _requestResponseSettings;
        private readonly ICheckpointTrigger _checkpointTrigger;

        public PartitionConsumerForResponses(EventHubMessageBus messageBus, RequestResponseSettings requestResponseSettings)
            : base(messageBus)
        {
            _logger = messageBus.LoggerFactory.CreateLogger<PartitionConsumerForResponses>();
            _requestResponseSettings = requestResponseSettings;
            _checkpointTrigger = new CheckpointTrigger(requestResponseSettings);
        }

        #region Overrides of EventProcessor

        protected override bool OnSubmit(EventData message, PartitionContext context)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Message submitted: {0}", new MessageContextInfo(context, message));
            }
            try
            {
                MessageBus.OnResponseArrived(message.Body.Array, _requestResponseSettings.Topic).Wait();
            }
            catch (Exception e)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(e, "Error occured while consuming response message, {0}", new MessageContextInfo(context, message));
                }

                // We can only continue and process all messages in the lease    

                if (_requestResponseSettings.OnResponseMessageFault != null)
                {
                    // Call the hook
                    _logger.LogDebug("Executing the attached hook from {0}", nameof(_requestResponseSettings.OnResponseMessageFault));
                    _requestResponseSettings.OnResponseMessageFault(_requestResponseSettings, message, e);
                }
            }
            return _checkpointTrigger.Increment();
        }

        protected override Task<MessageQueueResult<EventData>> OnCommit()
        {
            _logger.LogDebug("Commiting...");
            _checkpointTrigger.Reset();
            return Task.FromResult(new MessageQueueResult<EventData> { Success = true });
        }

        #endregion
    }
}