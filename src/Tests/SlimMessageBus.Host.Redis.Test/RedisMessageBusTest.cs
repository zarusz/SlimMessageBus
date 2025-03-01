namespace SlimMessageBus.Host.Redis.Test;

using System.Collections;
using System.Text;

using Newtonsoft.Json;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Serialization;

using StackExchange.Redis;

public class RedisMessageBusTest
{
    private readonly Lazy<RedisMessageBus> _subject;
    private readonly MessageBusSettings _settings = new();
    private readonly RedisMessageBusSettings _providerSettings;
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<IMessageSerializerProvider> _messageSerializerProviderMock = new();
    private readonly Mock<IMessageSerializer> _messageSerializerMock = new();

    private readonly Mock<IConnectionMultiplexer> _connectionMultiplexerMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ISubscriber> _subscriberMock;

    public RedisMessageBusTest()
    {
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(_messageSerializerProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        _serviceProviderMock.Setup(x => x.GetService(typeof(ICurrentTimeProvider))).Returns(new CurrentTimeProvider());
        _serviceProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        _serviceProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns(Enumerable.Empty<object>());
        _serviceProviderMock.Setup(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>))).Returns(Array.Empty<IMessageBusLifecycleInterceptor>());
        _serviceProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), new CurrentTimeProvider(), NullLoggerFactory.Instance));

        _settings.ServiceProvider = _serviceProviderMock.Object;

        _messageSerializerProviderMock.Setup(x => x.GetSerializer(It.IsAny<string>())).Returns(_messageSerializerMock.Object);

        _messageSerializerMock
            .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<object>()))
            .Returns((Type type, object message) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
        _messageSerializerMock
            .Setup(x => x.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()))
            .Returns((Type type, byte[] payload) => JsonConvert.DeserializeObject(Encoding.UTF8.GetString(payload), type));

        _databaseMock = new Mock<IDatabase>();
        _subscriberMock = new Mock<ISubscriber>();
        _connectionMultiplexerMock = new Mock<IConnectionMultiplexer>();
        _connectionMultiplexerMock.Setup(x => x.GetDatabase(-1, null)).Returns(() => _databaseMock.Object);
        _connectionMultiplexerMock.Setup(x => x.GetSubscriber(null)).Returns(() => _subscriberMock.Object);

        _providerSettings = new RedisMessageBusSettings("some string")
        {
            ConnectionFactory = () => _connectionMultiplexerMock.Object
        };

        _subject = new Lazy<RedisMessageBus>(() => new RedisMessageBus(_settings, _providerSettings));
    }

    private static ProducerSettings Producer(Type messageType, string defaultPath, PathKind kind)
    {
        var pb = new ProducerBuilder<object>(new ProducerSettings(), messageType);

        if (kind == PathKind.Topic)
        {
            pb.DefaultTopic(defaultPath);
        }
        else
        {
            pb.DefaultQueue(defaultPath);
        }

        return pb.Settings;
    }

    private MessageWithHeaders UnwrapPayload(RedisValue redisValue)
        => (MessageWithHeaders)_providerSettings.EnvelopeSerializer.Deserialize(typeof(MessageWithHeaders), redisValue);

    [Fact]
    public async Task When_Publish_Given_QueueAndTopic_Then_RoutesToRespectiveChannels()
    {
        // arrange
        const string topicA = "topic-a";
        const string queueB = "queue-b";

        _settings.Producers.Add(Producer(typeof(SomeMessageA), topicA, PathKind.Topic));
        _settings.Producers.Add(Producer(typeof(SomeMessageB), queueB, PathKind.Queue));

        var mA = new SomeMessageA { Value = Guid.NewGuid() };
        var mB = new SomeMessageB { Value = Guid.NewGuid() };

        var payloadA = _messageSerializerMock.Object.Serialize(typeof(SomeMessageA), mA);
        var payloadB = _messageSerializerMock.Object.Serialize(typeof(SomeMessageB), mB);

        // act
        await _subject.Value.ProducePublish(mA);
        await _subject.Value.ProducePublish(mB);

        // assert
        _databaseMock.Verify(x => x.PublishAsync(It.Is<RedisChannel>(channel => channel == topicA), It.Is<RedisValue>(x => StructuralComparisons.StructuralEqualityComparer.Equals(UnwrapPayload(x).Payload, payloadA)), It.IsAny<CommandFlags>()), Times.Once);
        _databaseMock.Verify(x => x.PublishAsync(It.Is<RedisChannel>(channel => channel == queueB), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);

        _databaseMock.Verify(x => x.ListRightPushAsync(It.Is<RedisKey>(key => key == queueB),
            It.Is<RedisValue>(x => StructuralComparisons.StructuralEqualityComparer.Equals(UnwrapPayload(x).Payload, payloadB)),
            When.Always,
            CommandFlags.None),
            Times.Once);

        _databaseMock.Verify(x => x.ListRightPushAsync(It.Is<RedisKey>(key => key == topicA),
            It.IsAny<RedisValue>(),
            When.Always,
            CommandFlags.None),
            Times.Never);
    }
}

public class SomeMessageA
{
    public Guid Value { get; set; }

    #region Equality members

    protected bool Equals(SomeMessageA other)
    {
        return Value.Equals(other.Value);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((SomeMessageA)obj);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    #endregion
}

public class SomeMessageB
{
    public Guid Value { get; set; }

    #region Equality members

    protected bool Equals(SomeMessageB other)
    {
        return Value.Equals(other.Value);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((SomeMessageB)obj);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    #endregion
}

public class SomeMessageAConsumer : IConsumer<SomeMessageA>, IDisposable
{
    public virtual void Dispose()
    {
        // Needed to check disposing
        GC.SuppressFinalize(this);
    }

    #region Implementation of IConsumer<in SomeMessageA>

    public virtual Task OnHandle(SomeMessageA messageA, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion
}

public class SomeMessageAConsumer2 : IConsumer<SomeMessageA>
{
    #region Implementation of IConsumer<in SomeMessageA>

    public virtual Task OnHandle(SomeMessageA messageA, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion
}

public class SomeMessageBConsumer : IConsumer<SomeMessageB>
{
    #region Implementation of IConsumer<in SomeMessageB>

    public virtual Task OnHandle(SomeMessageB message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion
}
