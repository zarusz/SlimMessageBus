using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public class MessageContextInfo
    {
        public readonly string Group;
        public readonly Message Message;

        public MessageContextInfo(string group, Message message)
        {
            Group = group;
            Message = message;
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"Group: {Group}, Topic: {Message.Topic}, Partition: {Message.Partition}, Offset: {Message.Offset}";
        }

        #endregion
    }
}


