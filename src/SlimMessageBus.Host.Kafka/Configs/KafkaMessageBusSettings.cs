using System;
using Confluent.Kafka;
using IProducer = Confluent.Kafka.IProducer<byte[], byte[]>;
using IConsumer = Confluent.Kafka.IConsumer<Confluent.Kafka.Ignore, byte[]>;

namespace SlimMessageBus.Host.Kafka
{
    public class KafkaMessageBusSettings
    {
        /// <summary>
        /// The Kafka broker nodes. This corresponds to "bootstrap.servers" Kafka setting.
        /// </summary>
        public string BrokerList { get; set; }
        /// <summary>
        /// Factory method that creates a <see cref="Producer"/> based on the supplied settings.
        /// </summary>
        public Func<ProducerConfig, IProducer> ProducerFactory { get; set; }
        /// <summary>
        /// Factory method that created a <see cref="Producer"/>.
        /// See also: https://kafka.apache.org/documentation/#producerconfigs
        /// </summary>
        public Func<ProducerConfig> ProducerConfigFactory { get; set; }
        /// <summary>
        /// Factory method that creates a <see cref="Consumer"/> based on the supplied settings and consumer GroupId. 
        /// </summary>
        public Func<string, ConsumerConfig, IConsumer> ConsumerFactory { get; set; }
        /// <summary>
        /// Factory method that creates settings based on the consumer GroupId.
        /// See also: https://kafka.apache.org/documentation/#newconsumerconfigs
        /// </summary>
        public Func<string, ConsumerConfig> ConsumerConfigFactory { get; set; }
        /// <summary>
        /// The timespan of the Poll kafka consumer operation before it times out.
        /// </summary>
        public TimeSpan ConsumerPollInterval { get; set; }
        /// <summary>
        /// The timespan of the Poll kafka consumer operation before it times out.
        /// </summary>
        public TimeSpan ConsumerPollRetryInterval { get; set; }

        public KafkaMessageBusSettings(string brokerList)
        {
            BrokerList = brokerList;
            ProducerConfigFactory = () => new ProducerConfig();
            ProducerFactory = (config) => new ProducerBuilder<byte[], byte[]>(config).Build();
            ConsumerConfigFactory = (group) => new ConsumerConfig { GroupId = group };
            ConsumerFactory = (group, config) => new ConsumerBuilder<Ignore, byte[]>(config).Build();
            ConsumerPollInterval = TimeSpan.FromSeconds(2);
            ConsumerPollRetryInterval = TimeSpan.FromSeconds(2);
        }
    }
}