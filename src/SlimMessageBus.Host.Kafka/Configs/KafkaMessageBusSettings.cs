namespace SlimMessageBus.Host.Kafka
{
    using System;
    using Confluent.Kafka;
    using ProducerBuilder = Confluent.Kafka.ProducerBuilder<byte[], byte[]>;
    using ConsumerBuilder = Confluent.Kafka.ConsumerBuilder<Confluent.Kafka.Ignore, byte[]>;
    using SlimMessageBus.Host.Serialization;

    public class KafkaMessageBusSettings
    {
        /// <summary>
        /// The Kafka broker nodes. This corresponds to "bootstrap.servers" Kafka setting.
        /// </summary>
        public string BrokerList { get; set; }
        /// <summary>
        /// Factory method that creates a <see cref="ProducerBuilder"/> based on the supplied settings.
        /// </summary>
        public Func<ProducerConfig, ProducerBuilder> ProducerBuilderFactory { get; set; }
        /// <summary>
        /// Factory method that created a <see cref="Producer"/>.
        /// See also: https://kafka.apache.org/documentation/#producerconfigs
        /// </summary>
        public Action<ProducerConfig> ProducerConfig { get; set; }
        /// <summary>
        /// Factory method that creates a <see cref="ConsumerBuilder"/> based on the supplied settings and consumer GroupId. 
        /// </summary>
        public Func<ConsumerConfig, ConsumerBuilder> ConsumerBuilderFactory { get; set; }
        /// <summary>
        /// Factory method that creates settings based on the consumer GroupId.
        /// See also: https://kafka.apache.org/documentation/#newconsumerconfigs
        /// </summary>
        public Action< ConsumerConfig> ConsumerConfig { get; set; }
        /// <summary>
        /// The timespan of the Poll retry when kafka consumer Poll operation errors out.
        /// </summary>
        public TimeSpan ConsumerPollRetryInterval { get; set; }

        /// <summary>
        /// Serializer used to serialize Kafka message header values. If not specified the default serializer will be used (setup as part of the bus config).
        /// </summary>
        public IMessageSerializer HeaderSerializer { get; set; }

        /// <summary>
        /// Should the commit on partitions for the consumed messages happen when the bus is stopped (or disposed)?
        /// This ensures the message reprocessing is minimized in between application restarts.
        /// Default is true.
        /// </summary>
        public bool EnableCommitOnBusStop { get; set; } = true;

        public KafkaMessageBusSettings(string brokerList)
        {
            BrokerList = brokerList;
            ProducerConfig = (config) => { };
            ProducerBuilderFactory = (config) => new ProducerBuilder<byte[], byte[]>(config);
            ConsumerConfig = (config) => { };
            ConsumerBuilderFactory = (config) => new ConsumerBuilder<Ignore, byte[]>(config);
            ConsumerPollRetryInterval = TimeSpan.FromSeconds(2);
        }
    }
}