namespace SlimMessageBus.Host.Kafka
{
    using System;
    using ConsumeResult = Confluent.Kafka.ConsumeResult<Confluent.Kafka.Ignore, byte[]>;

    public static class KafkaConsumerContextExtensions
    {
        private const string MessageKey = "Kafka_Message";

        public static ConsumeResult GetTransportMessage(this IConsumerContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            return context.GetPropertyOrDefault<ConsumeResult>(MessageKey);
        }

        public static void SetTransportMessage(this ConsumerContext context, ConsumeResult message)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            context.SetProperty(MessageKey, message);
        }
    }
}
