using Confluent.Kafka;
using System;

namespace SlimMessageBus.Host.Kafka
{
    public static class KafkaConsumerContextExtensions
    {
        private const string MessageKey = "Kafka_Message";

        public static Message<Null, byte[]> GetTransportMessage(this ConsumerContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            return context.GetOrDefault<Message<Null, byte[]>>(MessageKey, null);
        }

        public static void SetTransportMessage(this ConsumerContext context, Message<Null, byte[]> message)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            context.Properties[MessageKey] = message;
        }
    }
}
