namespace SlimMessageBus.Host.Redis.Test
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Moq;
    using Newtonsoft.Json;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Serialization;
    using StackExchange.Redis;
    using Xunit;

    public class RedisMessageBusTest
    {
        private readonly Lazy<RedisMessageBus> _subject;
        private readonly MessageBusSettings _settings = new();
        private readonly RedisMessageBusSettings _providerSettings;
        private readonly Mock<IDependencyResolver> _dependencyResolverMock = new();
        private readonly Mock<IMessageSerializer> _messageSerializerMock = new();

        private readonly Mock<IConnectionMultiplexer> _connectionMultiplexerMock;
        private readonly Mock<IDatabase> _databaseMock;
        private readonly Mock<ISubscriber> _subscriberMock;

        public RedisMessageBusTest()
        {
            _dependencyResolverMock.Setup(x => x.Resolve(It.IsAny<Type>())).Returns((Type t) =>
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return Enumerable.Empty<object>();
                return null;
            });

            _settings.DependencyResolver = _dependencyResolverMock.Object;
            _settings.Serializer = _messageSerializerMock.Object;

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

            _providerSettings = new RedisMessageBusSettings("")
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

        private static ConsumerSettings Consumer(Type messageType, string topic, Type consumerType)
        {
            return new ConsumerBuilder<object>(new MessageBusSettings(), messageType).Topic(topic).WithConsumer(consumerType).ConsumerSettings;
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
            await _subject.Value.Publish(mA);
            await _subject.Value.Publish(mB);

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
            if (ReferenceEquals(null, obj)) return false;
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
            if (ReferenceEquals(null, obj)) return false;
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
        }

        #region Implementation of IConsumer<in SomeMessageA>

        public virtual Task OnHandle(SomeMessageA messageA, string name)
        {
            return Task.CompletedTask;
        }

        #endregion
    }

    public class SomeMessageAConsumer2 : IConsumer<SomeMessageA>
    {
        #region Implementation of IConsumer<in SomeMessageA>

        public virtual Task OnHandle(SomeMessageA messageA, string name)
        {
            return Task.CompletedTask;
        }

        #endregion
    }

    public class SomeMessageBConsumer : IConsumer<SomeMessageB>
    {
        #region Implementation of IConsumer<in SomeMessageB>

        public virtual Task OnHandle(SomeMessageB message, string name)
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
