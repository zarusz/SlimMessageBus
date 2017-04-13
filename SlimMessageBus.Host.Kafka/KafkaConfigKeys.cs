namespace SlimMessageBus.Host.Kafka
{
    public static class KafkaConfigKeys
    {
        public const string Servers = "bootstrap.servers";

        public static class Consumer
        {
            public const string GroupId = "group.id";
            public const string EnableAutoCommit = "enable.auto.commit";
            public const string AutoCommitEnableMs = "auto.commit.interval.ms";
            public const string StatisticsIntervalMs = "statistics.interval.ms";
            public const string AutoOffsetReset = "auto.offset.reset";

        }
    }
}