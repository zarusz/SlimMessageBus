namespace SlimMessageBus.Host.Kafka
{
    public static class KafkaConfigKeys
    {
        public const string Servers = "metadata.broker.list"; //"bootstrap.servers";

        public static class ConsumerKeys
        {
            public const string GroupId = "group.id";
            public const string EnableAutoCommit = "enable.auto.commit";
            public const string AutoCommitEnableMs = "auto.commit.interval.ms";
            public const string StatisticsIntervalMs = "statistics.interval.ms";
            public const string AutoOffsetReset = "auto.offset.reset";
        }
    }
}