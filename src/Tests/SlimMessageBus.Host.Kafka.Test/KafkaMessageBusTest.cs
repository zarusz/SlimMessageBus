﻿namespace SlimMessageBus.Host.Kafka.Test
{
    using Confluent.Kafka;
    using FluentAssertions;
    using Moq;
    using SlimMessageBus.Host.Config;
    using System;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Serialization;
    using Xunit;
    using Microsoft.Extensions.Logging.Abstractions;

    public class KafkaMessageBusTest : IDisposable
    {
        private MessageBusSettings MbSettings { get; }
        private KafkaMessageBusSettings KafkaMbSettings { get; }
        private Lazy<WrappedKafkaMessageBus> KafkaMb { get; }

        public KafkaMessageBusTest()
        {
            var producerMock = new Mock<IProducer<byte[], byte[]>>();
            producerMock.SetupGet(x => x.Name).Returns("Producer Name");

            var producerBuilderMock = new Mock<ProducerBuilder<byte[], byte[]>>(new ProducerConfig());
            producerBuilderMock.Setup(x => x.Build()).Returns(producerMock.Object);

            MbSettings = new MessageBusSettings
            {
                Serializer = new Mock<IMessageSerializer>().Object,
                DependencyResolver = new Mock<IDependencyResolver>().Object,
                LoggerFactory = NullLoggerFactory.Instance
            };
            KafkaMbSettings = new KafkaMessageBusSettings("host")
            {
                ProducerBuilderFactory = (config) => producerBuilderMock.Object
            };
            KafkaMb = new Lazy<WrappedKafkaMessageBus>(() => new WrappedKafkaMessageBus(MbSettings, KafkaMbSettings));
        }

        public void Dispose()
        {
            KafkaMb.Value.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void GetMessageKey()
        {
            // arrange
            var producerA = new ProducerSettings();
            new ProducerBuilder<MessageA>(producerA)
                .KeyProvider((m, t) => m.Key);

            var producerB = new ProducerSettings();
            new ProducerBuilder<MessageB>(producerB);

            MbSettings.Producers.Add(producerA);
            MbSettings.Producers.Add(producerB);        

            var msgA = new MessageA();
            var msgB = new MessageB();

            // act
            var msgAKey = KafkaMb.Value.Public_GetMessageKey(msgA.GetType(), msgA, "topic1");
            var msgBKey = KafkaMb.Value.Public_GetMessageKey(msgB.GetType(), msgB, "topic1");

            // assert
            msgAKey.Should().BeSameAs(msgA.Key);
            msgBKey.Should().BeNull();
        }

        [Fact]
        public void GetMessagePartition()
        {
            // arrange
            var publisherA = new ProducerSettings();
            new ProducerBuilder<MessageA>(publisherA)
                .PartitionProvider((m, t) => 10);

            var publisherB = new ProducerSettings();
            new ProducerBuilder<MessageB>(publisherB);

            MbSettings.Producers.Add(publisherA);
            MbSettings.Producers.Add(publisherB);

            var msgA = new MessageA();
            var msgB = new MessageB();

            // act
            var msgAPartition = KafkaMb.Value.Public_GetMessagePartition(msgA.GetType(), msgA, "topic1");
            var msgBPartition = KafkaMb.Value.Public_GetMessagePartition(msgB.GetType(), msgB, "topic1");

            // assert
            msgAPartition.Should().Be(10);
            msgBPartition.Should().Be(-1);
        }

        class MessageA
        {
            public byte[] Key { get; } = Guid.NewGuid().ToByteArray();
        }

        class MessageB
        {
        }

        class WrappedKafkaMessageBus : KafkaMessageBus
        {
            public WrappedKafkaMessageBus(MessageBusSettings settings, KafkaMessageBusSettings kafkaSettings)
                : base(settings, kafkaSettings)
            {
            }

            public byte[] Public_GetMessageKey(Type messageType, object message, string topic)
            {
                return GetMessageKey(messageType, message, topic);
            }

            public int Public_GetMessagePartition(Type messageType, object message, string topic)
            {
                return GetMessagePartition(messageType, message, topic);
            }
        }
    }
}
