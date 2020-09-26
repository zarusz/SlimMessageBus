using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public class MessageProcessingResult<TMessage> where TMessage : class
    {
        public Task Task { get; }
        public TMessage Message { get; }

        public MessageProcessingResult(Task task, TMessage message)
        {
            Task = task;
            Message = message;
        }
    }
}