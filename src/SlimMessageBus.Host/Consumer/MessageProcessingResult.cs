using System.Threading.Tasks;

namespace SlimMessageBus.Host
{
    public class MessageProcessingResult<TMessage>
        where TMessage : class
    {
        public readonly Task Task;
        public readonly TMessage Message;

        public MessageProcessingResult(Task task, TMessage message)
        {
            Task = task;
            Message = message;
        }
    }
}