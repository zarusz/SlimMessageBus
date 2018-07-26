using Confluent.Kafka;
using System.Threading.Tasks;

namespace SlimMessageBus.Host.Kafka
{
    public interface IKafkaCommitController
    {
        Task Commit(TopicPartitionOffset offset);
    }
}