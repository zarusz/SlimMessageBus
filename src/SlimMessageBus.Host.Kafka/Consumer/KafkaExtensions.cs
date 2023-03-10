namespace SlimMessageBus.Host.Kafka;

using System.Diagnostics.CodeAnalysis;

using SlimMessageBus.Host.Serialization;

public static class KafkaExtensions
{
    public static TopicPartitionOffset AddOffset([NotNull] this TopicPartitionOffset topicPartitionOffset, int addOffset)
        => new(topicPartitionOffset.TopicPartition, topicPartitionOffset.Offset + addOffset);

    public static IReadOnlyDictionary<string, object> ToHeaders(this ConsumeResult<Ignore, byte[]> consumeResult, IMessageSerializer headerSerializer)
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
}


