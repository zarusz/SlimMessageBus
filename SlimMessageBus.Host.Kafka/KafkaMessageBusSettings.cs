using System;
using System.Collections.Generic;
using Confluent.Kafka;

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
        public Func<IDictionary<string, object>, Producer> ProducerFactory { get; set; }
        /// <summary>
        /// Factory method that created a <see cref="Producer"/>.
        /// See also: https://kafka.apache.org/documentation/#producerconfigs
        /// </summary>
        public Func<IDictionary<string, object>> ProducerConfigFactory { get; set; }
        /// <summary>
        /// Factory method that creates a <see cref="Consumer"/> based on the supplied settings and consumer GroupId. 
        /// </summary>
        public Func<string, IDictionary<string, object>, Consumer> ConsumerFactory { get; set; }
        /// <summary>
        /// Factory method that creates settings based on the consumer GroupId.
        /// See also: https://kafka.apache.org/documentation/#newconsumerconfigs
        /// </summary>
        public Func<string, IDictionary<string, object>> ConsumerConfigFactory { get; set; }

        public KafkaMessageBusSettings(string brokerList)
        {
            BrokerList = brokerList;
            ProducerConfigFactory = () => new Dictionary<string, object>();
            ProducerFactory = (config) => new Producer(config);
            ConsumerConfigFactory = (group) => new Dictionary<string, object>();
            ConsumerFactory = (group, config) => new Consumer(config);
        }
    }
}