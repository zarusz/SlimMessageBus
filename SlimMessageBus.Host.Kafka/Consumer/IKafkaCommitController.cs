using System.Threading.Tasks;
using Confluent.Kafka;

namespace SlimMessageBus.Host.Kafka
{
    public interface IKafkaCommitController
    {
        Task Commit(TopicPartitionOffset offset);
    }
}