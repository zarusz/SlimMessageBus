namespace SlimMessageBus.Host.Kafka
{
    public static class KafkaConfigValues
    {
        /// <summary>
        /// Check out the https://kafka.apache.org/documentation/#newconsumerconfigs
        /// </summary>
        public static class AutoOffsetReset
        {
            public const string Latest = "latest";
            public const string Earliest = "earliest";
            public const string None = "none";
        }
    }
}