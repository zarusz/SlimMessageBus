using System.Threading.Tasks;
using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public class MessageProcessingResult
    {
        public readonly Task Task;
        public readonly Message Message;

        public MessageProcessingResult(Task task, Message message)
        {
            Task = task;
            Message = message;
        }
    }
}