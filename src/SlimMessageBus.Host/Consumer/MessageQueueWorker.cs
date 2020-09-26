using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public class MessageQueueWorker<TMessage> where TMessage : class
    {
        private readonly ILogger _logger;

        private readonly Queue<MessageProcessingResult<TMessage>> _pendingMessages = new Queue<MessageProcessingResult<TMessage>>();

        public int Count => _pendingMessages.Count;
        public ConsumerInstancePoolMessageProcessor<TMessage> ConsumerInstancePool { get; }

        private readonly ICheckpointTrigger _checkpointTrigger;

        public MessageQueueWorker(ConsumerInstancePoolMessageProcessor<TMessage> consumerInstancePool, ICheckpointTrigger checkpointTrigger, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MessageQueueWorker<TMessage>>();
            ConsumerInstancePool = consumerInstancePool ?? throw new ArgumentNullException(nameof(consumerInstancePool));
            _checkpointTrigger = checkpointTrigger ?? throw new ArgumentNullException(nameof(checkpointTrigger));
        }

        /// <summary>
        /// Clears the pending messages
        /// </summary>
        public virtual void Clear()
        {
            _pendingMessages.Clear();
        }

        /// <summary>
        /// Submits an incoming message to the queue to be processed
        /// </summary>
        /// <param name="message">The message to be processed</param>
        /// <returns>True if should Commit() at this point.</returns>
        public virtual bool Submit(TMessage message)
        {
            if (_pendingMessages.Count == 0)
            {
                // when message arrives for the first time (or since last commit)
                _checkpointTrigger.Reset();
            }

            var messageTask = ConsumerInstancePool.ProcessMessage(message);
            _pendingMessages.Enqueue(new MessageProcessingResult<TMessage>(messageTask, message));

            // limit check / time check
            return _checkpointTrigger.Increment();
        }

        /// <summary>
        /// Wait until all pending message processing is finished:
        /// - checks if any failed
        /// - clears the checkpoint state and internal queue
        /// </summary>
        public virtual async Task<MessageQueueResult<TMessage>> WaitAll()
        {
            var result = new MessageQueueResult<TMessage>
            {
                Success = true
            };

            if (_pendingMessages.Count > 0)
            {
                try
                {
                    var tasks = _pendingMessages.Select(x => x.Task);
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // some tasks failed
                    result.Success = false;
                    _logger.LogError(e, "Errors occured while executing the tasks");

                    // grab last message that succeeded (if any)
                    // Note: Assumption that that messages in queue follow the partition offset.
                    foreach (var messageProcessingResult in _pendingMessages)
                    {
                        if (messageProcessingResult.Task.IsFaulted || messageProcessingResult.Task.IsCanceled)
                        {
                            break;
                        }
                        result.LastSuccessMessage = messageProcessingResult.Message;
                    }
                }
                _pendingMessages.Clear();
            }
            return result;
        }
    }
}