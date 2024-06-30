namespace SlimMessageBus.Host.Kafka;

using Confluent.Kafka;

public class KafkaMessageBusSettings
{
    /// <summary>
    /// The Kafka broker nodes. This corresponds to "bootstrap.servers" Kafka setting.
    /// </summary>
    public string BrokerList { get; set; }

    /// <summary>
    /// Factory method that creates a <see cref="ProducerBuilder"/> based on the supplied config.
    /// </summary>
    public Func<ProducerConfig, ProducerBuilder> ProducerBuilderFactory { get; set; }

    /// <summary>
    /// Delegate that configured the <see cref="ProducerConfig"/> object.
    /// See also: https://kafka.apache.org/documentation/#producerconfigs
    /// </summary>
    public Action<ProducerConfig> ProducerConfig { get; set; }

    /// <summary>
    /// Factory method that creates a <see cref="ConsumerBuilder"/> based on the supplied config. 
    /// </summary>
    public Func<ConsumerConfig, ConsumerBuilder> ConsumerBuilderFactory { get; set; }

    /// <summary>
    /// Delegate that configured the <see cref="ConsumerConfig"/> object.
    /// See also: https://kafka.apache.org/documentation/#newconsumerconfigs
    /// </summary>
    public Action<ConsumerConfig> ConsumerConfig { get; set; }

    /// <summary>
    /// Factory method that creates a <see cref="AdminClientBuilder"/> based on the supplied config. 
    /// </summary>
    public Func<AdminClientConfig, AdminClientBuilder> AdminClientBuilderFactory { get; set; }

    /// <summary>
    /// Delegate that configured the <see cref="AdminClientConfig"/> object.
    /// </summary>
    public Action<AdminClientConfig> AdminClientConfig { get; set; }

    /// <summary>
    /// The timespan of the Poll retry when kafka consumer Poll operation errors out.
    /// </summary>
    public TimeSpan ConsumerPollRetryInterval { get; set; }

    /// <summary>
    /// Serializer used to serialize Kafka message header values. If not specified the default serializer will be used (setup as part of the bus config). By default the <see cref="DefaultKafkaHeaderSerializer"/> is used.
    /// </summary>
    public IMessageSerializer HeaderSerializer { get; set; }

    /// <summary>
    /// Should the commit on partitions for the consumed messages happen when the bus is stopped (or disposed)?
    /// This ensures the message reprocessing is minimized in between application restarts.
    /// Default is true.
    /// </summary>
    public bool EnableCommitOnBusStop { get; set; } = true;

    /// <summary>
    /// Settings for auto creation of topics.
    /// </summary>
    public KafkaBusTopologySettings TopologyProvisioning { get; set; }

    public KafkaMessageBusSettings()
    {
        ProducerConfig = (config) => { };
        ProducerBuilderFactory = (config) => new ProducerBuilder<byte[], byte[]>(config);

        ConsumerConfig = (config) => { };
        ConsumerBuilderFactory = (config) => new ConsumerBuilder<Ignore, byte[]>(config);

        AdminClientConfig = (config) => { };
        AdminClientBuilderFactory = (config) => new AdminClientBuilder(config);

        ConsumerPollRetryInterval = TimeSpan.FromSeconds(2);

        HeaderSerializer = new DefaultKafkaHeaderSerializer();

        TopologyProvisioning = new KafkaBusTopologySettings();
    }

    public KafkaMessageBusSettings(string brokerList) : this()
    {
        BrokerList = brokerList;
    }
}
