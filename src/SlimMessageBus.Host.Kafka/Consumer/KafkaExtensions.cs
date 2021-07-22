namespace SlimMessageBus.Host.Kafka
{
    using Confluent.Kafka;
    using SlimMessageBus.Host.Serialization;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    public static class KafkaExtensions
    {
        public static TopicPartitionOffset AddOffset([NotNull] this TopicPartitionOffset topicPartitionOffset, int addOffset)
            => new TopicPartitionOffset(topicPartitionOffset.TopicPartition, topicPartitionOffset.Offset + addOffset);

        public static IDictionary<string, object> ToHeaders(this ConsumeResult<Ignore, byte[]> consumeResult, IMessageSerializer headerSerializer)
        {
            if (consumeResult.Message.Headers == null)
            {
                return null;
            }

            // If message has headers then deserialize
            var headers = new Dictionary<string, object>();
            foreach (var header in consumeResult.Message.Headers)
            {
                var value = headerSerializer.Deserialize(typeof(object), header.GetValueBytes());
                headers[header.Key] = value;
            }

            return headers;
        }

        public static MessageWithHeaders ToMessageWithHeaders(this ConsumeResult<Ignore, byte[]> consumeResult, IMessageSerializer headerSerializer)
            => new MessageWithHeaders(consumeResult.Message.Value, consumeResult.ToHeaders(headerSerializer));
    }
}


