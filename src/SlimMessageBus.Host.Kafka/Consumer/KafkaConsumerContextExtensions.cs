using System;
using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

namespace SlimMessageBus.Host.Kafka
{
    public static class KafkaConsumerContextExtensions
    {
        private const string MessageKey = "Kafka_Message";

        public static ConsumeResult GetTransportMessage(this ConsumerContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            return context.GetOrDefault<ConsumeResult>(MessageKey, null);
        }

        public static void SetTransportMessage(this ConsumerContext context, ConsumeResult message)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            context.Properties[MessageKey] = message;
        }
    }
}
