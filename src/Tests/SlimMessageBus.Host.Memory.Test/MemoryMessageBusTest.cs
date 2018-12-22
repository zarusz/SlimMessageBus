using System;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization;
using Xunit;

namespace SlimMessageBus.Host.Memory.Test
{
    public class MemoryMessageBusTest
    {
        private readonly Lazy<MemoryMessageBus> _subject;
        private readonly MessageBusSettings _settings = new MessageBusSettings();
        private readonly MemoryMessageBusSettings _providerSettings = new MemoryMessageBusSettings();
        private readonly Mock<IDependencyResolver> _dependencyResolverMock = new Mock<IDependencyResolver>();
        private readonly Mock<IMessageSerializer> _messageSerializerMock = new Mock<IMessageSerializer>();

        public MemoryMessageBusTest()
        {
            _settings.DependencyResolver = _dependencyResolverMock.Object;
            _settings.Serializer = _messageSerializerMock.Object;

            _messageSerializerMock
                .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<object>()))
                .Returns((Type type, object message) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
            _messageSerializerMock
                .Setup(x => x.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()))
                .Returns((Type type, byte[] payload) => JsonConvert.DeserializeObject(Encoding.UTF8.GetString(payload), type));

            _subject = new Lazy<MemoryMessageBus>(() => new MemoryMessageBus(_settings, _providerSettings));
        }

        private static ProducerSettings Publisher(Type messageType, string defaultTopic)
        {
            return new ProducerSettings
            {
                MessageType = messageType,
                DefaultTopic = defaultTopic
            };
        }

        private static ConsumerSettings Consumer(Type messageType, string topic, Type consumerType)
        {
            return new ConsumerSettings
            {
                MessageType = messageType,
                Topic = topic,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = consumerType
            };
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_Publish_Given_MessageSerializationSetting_Then_DeliversMessageInstanceToRespectiveConsumers(bool enableMessageSerialization)
        {
            // arrange
            const string topicA = "topic-a";
            const string topicA2 = "topic-a-2";
            const string topicB = "topic-b";

            _settings.Producers.Add(Publisher(typeof(SomeMessageA), topicA));
            _settings.Producers.Add(Publisher(typeof(SomeMessageB), topicB));
            _settings.Consumers.Add(Consumer(typeof(SomeMessageA), topicA, typeof(SomeMessageAConsumer)));
            _settings.Consumers.Add(Consumer(typeof(SomeMessageA), topicA2, typeof(SomeMessageAConsumer2)));
            _settings.Consumers.Add(Consumer(typeof(SomeMessageB), topicB, typeof(SomeMessageBConsumer)));

            var aConsumerMock = new Mock<SomeMessageAConsumer>();
            var aConsumer2Mock = new Mock<SomeMessageAConsumer2>();
            var bConsumerMock = new Mock<SomeMessageBConsumer>();
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageAConsumer))).Returns(aConsumerMock.Object);
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageAConsumer2))).Returns(aConsumer2Mock.Object);
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageBConsumer))).Returns(bConsumerMock.Object);

            _providerSettings.EnableMessageSerialization = enableMessageSerialization;

            var m = new SomeMessageA();

            // act
            _subject.Value.Publish(m).Wait();

            // assert
            if (enableMessageSerialization)
            {
                aConsumerMock.Verify(x => x.OnHandle(It.Is<SomeMessageA>(a => a.Equals(m)), topicA), Times.Once);
            }
            else
            {
                aConsumerMock.Verify(x => x.OnHandle(m, topicA), Times.Once);
            }
            aConsumerMock.VerifyNoOtherCalls();

            aConsumer2Mock.Verify(x => x.OnHandle(It.IsAny<SomeMessageA>(), topicA2), Times.Never);
            aConsumer2Mock.VerifyNoOtherCalls();

            bConsumerMock.Verify(x => x.OnHandle(It.IsAny<SomeMessageB>(), topicB), Times.Never);
            bConsumerMock.VerifyNoOtherCalls();
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
            if (obj.GetType() != this.GetType()) return false;
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
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SomeMessageB)obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        #endregion
    }

    public class SomeMessageAConsumer : IConsumer<SomeMessageA>
    {
        #region Implementation of IConsumer<in SomeMessageA>

        public virtual Task OnHandle(SomeMessageA messageA, string topic)
        {
            return Task.CompletedTask;
        }

        #endregion
    }

    public class SomeMessageAConsumer2 : IConsumer<SomeMessageA>
    {
        #region Implementation of IConsumer<in SomeMessageA>

        public virtual Task OnHandle(SomeMessageA messageA, string topic)
        {
            return Task.CompletedTask;
        }

        #endregion
    }

    public class SomeMessageBConsumer : IConsumer<SomeMessageB>
    {
        #region Implementation of IConsumer<in SomeMessageB>

        public virtual Task OnHandle(SomeMessageB message, string topic)
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
