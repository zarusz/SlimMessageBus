
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Nesting here is desired, as it gives a nice API", Scope = "type", Target = "~T:SlimMessageBus.Host.Kafka.KafkaConfigKeys.ConsumerKeys")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Nesting here is desired, as it gives a nice API", Scope = "type", Target = "~T:SlimMessageBus.Host.Kafka.KafkaConfigValues.AutoOffsetReset")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The reference is kept and disposed with the parent owning object", Scope = "member", Target = "~M:SlimMessageBus.Host.Kafka.KafkaPartitionConsumerForConsumers.#ctor(SlimMessageBus.Host.Config.ConsumerSettings,Confluent.Kafka.TopicPartition,SlimMessageBus.Host.Kafka.IKafkaCommitController,SlimMessageBus.Host.MessageBusBase)")]
