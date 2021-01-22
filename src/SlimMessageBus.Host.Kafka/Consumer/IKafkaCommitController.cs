using System.Threading.Tasks;
using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public interface IKafkaCommitController
    {
        ValueTask Commit(TopicPartitionOffset offset);
    }
}