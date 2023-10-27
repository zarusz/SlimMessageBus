namespace SlimMessageBus.Host.Kafka.Test;

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

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<IMessageSerializer>))).CallBase();
        serviceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());

        MbSettings = new MessageBusSettings
        {
            ServiceProvider = serviceProviderMock.Object,
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
            .DefaultTopic("topic1")
            .KeyProvider((m, t) => m.Key);

        var producerB = new ProducerSettings();
        new ProducerBuilder<MessageB>(producerB)
            .DefaultTopic("topic1");

        MbSettings.Producers.Add(producerA);
        MbSettings.Producers.Add(producerB);

        var msgA = new MessageA();
        var msgB = new MessageB();

        // act
        var msgAKey = KafkaMb.Value.Public_GetMessageKey(producerA, msgA.GetType(), msgA, "topic1");
        var msgBKey = KafkaMb.Value.Public_GetMessageKey(producerB, msgB.GetType(), msgB, "topic1");

        // assert
        msgAKey.Should().BeSameAs(msgA.Key);
        msgBKey.Should().BeEmpty();
    }

    [Fact]
    public void GetMessagePartition()
    {
        // arrange
        var producerA = new ProducerSettings();
        new ProducerBuilder<MessageA>(producerA)
            .DefaultTopic("topic1")
            .PartitionProvider((m, t) => 10);

        var producerB = new ProducerSettings();
        new ProducerBuilder<MessageB>(producerB)
            .DefaultTopic("topic1");

        MbSettings.Producers.Add(producerA);
        MbSettings.Producers.Add(producerB);

        var msgA = new MessageA();
        var msgB = new MessageB();

        // act
        var msgAPartition = KafkaMb.Value.Public_GetMessagePartition(producerA, msgA.GetType(), msgA, "topic1");
        var msgBPartition = KafkaMb.Value.Public_GetMessagePartition(producerB, msgB.GetType(), msgB, "topic1");

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

        public byte[] Public_GetMessageKey(ProducerSettings producerSettings, Type messageType, object message, string topic)
            => GetMessageKey(producerSettings, messageType, message, topic);

        public int Public_GetMessagePartition(ProducerSettings producerSettings, Type messageType, object message, string topic)
            => GetMessagePartition(producerSettings, messageType, message, topic);
    }
}
