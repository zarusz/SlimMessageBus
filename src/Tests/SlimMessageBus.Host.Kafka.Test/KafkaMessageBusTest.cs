namespace SlimMessageBus.Host.Kafka.Test;

using System.Linq.Expressions;
using System.Threading.Tasks;

using Confluent.Kafka;

using SlimMessageBus.Host.Collections;

public class KafkaMessageBusTest : IDisposable
{
    private readonly Mock<ProducerBuilder<byte[], byte[]>> _producerBuilderMock;
    private readonly Mock<IProducer<byte[], byte[]>> _producerMock;

    private MessageBusSettings MbSettings { get; }
    private KafkaMessageBusSettings KafkaMbSettings { get; }
    private Lazy<WrappedKafkaMessageBus> KafkaMb { get; }

    public KafkaMessageBusTest()
    {
        _producerMock = new Mock<IProducer<byte[], byte[]>>();
        _producerMock.SetupGet(x => x.Name).Returns("Producer Name");

        _producerBuilderMock = new Mock<ProducerBuilder<byte[], byte[]>>(new ProducerConfig());
        _producerBuilderMock.Setup(x => x.Build()).Returns(_producerMock.Object);

        var messageSerializerMock = new Mock<IMessageSerializer>();

        var messageSerializerProviderMock = new Mock<IMessageSerializerProvider>();
        messageSerializerProviderMock.Setup(x => x.GetSerializer(It.IsAny<string>())).Returns(messageSerializerMock.Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<IMessageSerializer>))).CallBase();
        serviceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        serviceProviderMock.Setup(x => x.GetService(typeof(ICurrentTimeProvider))).Returns(new CurrentTimeProvider());
        serviceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(messageSerializerProviderMock.Object);
        serviceProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Array.CreateInstance(t.GetGenericArguments()[0], 0));
        serviceProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        serviceProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), new CurrentTimeProvider(), NullLoggerFactory.Instance));

        MbSettings = new MessageBusSettings
        {
            ServiceProvider = serviceProviderMock.Object,
        };
        KafkaMbSettings = new KafkaMessageBusSettings("host")
        {
            ProducerBuilderFactory = (config) => _producerBuilderMock.Object
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

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task When_Publish_Given_EnableAwaitProduce_Then_UsesSyncOrAsyncProduceVersionAsync(bool enableAwaitProduce, bool withPartitionProvider)
    {
        // arrange
        var topic = "topic1";
        var producer = new ProducerSettings();
        var producerBuilder = new ProducerBuilder<SomeMessage>(producer)
            .DefaultTopic(topic)
            .EnableProduceAwait(enableAwaitProduce);

        var partitionNumber = 10;
        if (withPartitionProvider)
        {
            producerBuilder.PartitionProvider((m, t) => partitionNumber);
        }

        MbSettings.Producers.Add(producer);

        var cancellationToken = new CancellationTokenSource().Token;
        var msg = new SomeMessage();

        var deliveryReport = new DeliveryResult<byte[], byte[]> { Status = PersistenceStatus.Persisted };
        _producerMock
            .Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<byte[], byte[]>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deliveryReport);
        _producerMock
            .Setup(x => x.ProduceAsync(It.IsAny<TopicPartition>(), It.IsAny<Message<byte[], byte[]>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deliveryReport);

        // act
        await KafkaMb.Value.ProducePublish(msg, cancellationToken: cancellationToken);

        // assert
        Expression<Action<IProducer<byte[], byte[]>>> exp;
        if (enableAwaitProduce)
        {
            exp = withPartitionProvider
                ? x => x.ProduceAsync(It.Is<TopicPartition>(tp => tp.Partition == partitionNumber && tp.Topic == topic), It.IsAny<Message<byte[], byte[]>>(), It.IsAny<CancellationToken>())
                : x => x.ProduceAsync(topic, It.IsAny<Message<byte[], byte[]>>(), It.IsAny<CancellationToken>());
        }
        else
        {
            exp = withPartitionProvider
                ? x => x.Produce(It.Is<TopicPartition>(tp => tp.Partition == partitionNumber && tp.Topic == topic), It.IsAny<Message<byte[], byte[]>>(), It.IsAny<Action<DeliveryReport<byte[], byte[]>>>())
                : x => x.Produce(topic, It.IsAny<Message<byte[], byte[]>>(), It.IsAny<Action<DeliveryReport<byte[], byte[]>>>());
        }
        _producerMock.Verify(exp, Times.Once);
        _producerMock.VerifyNoOtherCalls();
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
