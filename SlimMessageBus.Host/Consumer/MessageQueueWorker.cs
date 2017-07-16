using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;

namespace SlimMessageBus.Host
{
    public class MessageQueueWorker<TMessage>
        where TMessage : class
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MessageQueueWorker<TMessage>));

        private readonly Queue<MessageProcessingResult<TMessage>> _pendingMessages = new Queue<MessageProcessingResult<TMessage>>();
        private readonly ConsumerInstancePool<TMessage> _consumerInstancePool;

        public MessageQueueWorker(ConsumerInstancePool<TMessage> consumerInstancePool)
        {
            _consumerInstancePool = consumerInstancePool;
        }

        public void Submit(TMessage message)
        {
            var messageTask = _consumerInstancePool.ProcessMessage(message);
            _pendingMessages.Enqueue(new MessageProcessingResult<TMessage>(messageTask, message));
        }

        public TMessage Commit(TMessage lastMessage)
        {
            if (_pendingMessages.Count > 0)
            {
                try
                {
                    var tasks = _pendingMessages.Select(x => x.Task).ToArray();
                    Task.WaitAll(tasks);
                }
                catch (AggregateException e)
                {
                    Log.ErrorFormat("Errors occured while executing the tasks.", e);
                    // ToDo: some tasks failed
                }
                _pendingMessages.Clear();
            }
            return lastMessage;
        }
    }
}