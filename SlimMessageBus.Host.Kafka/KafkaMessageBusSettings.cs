namespace SlimMessageBus.Host.Kafka
{
    public class KafkaMessageBusSettings
    {
        public string BrokerList { get; set; }

        public KafkaMessageBusSettings(string brokerList)
        {
            BrokerList = brokerList;
        }
    }
}