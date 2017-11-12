using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public static class KafkaConsumerContextExtensions
    {
        private const string MessageKey = "Kafka_Message";

        public static Message GetTransportMessage(this ConsumerContext context)
        {
            return context.GetOrDefault<Message>(MessageKey, null);
        }

        public static void SetTransportMessage(this ConsumerContext context, Message message)
        {
            context.Properties[MessageKey] = message;
        }
    }
}
