using System;
using System.Threading.Tasks;
using Moq;
using SlimMessageBus.Host.Config;
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

            _subject = new Lazy<MemoryMessageBus>(() => new MemoryMessageBus(_settings, _providerSettings));
        }

        private static PublisherSettings Publisher(Type messageType, string defaultTopic)
        {
            return new PublisherSettings
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

        [Fact]
        public void PublishDeliversSameMessageInstanceToRespectiveConsumers()
        {
            // arrange
            var topicA = "topic-a";
            var topicA2 = "topic-a-2";
            var topicB = "topic-b";

            _settings.Publishers.Add(Publisher(typeof(SomeMessageA), topicA));
            _settings.Publishers.Add(Publisher(typeof(SomeMessageB), topicB));
            _settings.Consumers.Add(Consumer(typeof(SomeMessageA), topicA, typeof(SomeMessageAConsumer)));
            _settings.Consumers.Add(Consumer(typeof(SomeMessageA), topicA2, typeof(SomeMessageAConsumer2)));
            _settings.Consumers.Add(Consumer(typeof(SomeMessageB), topicB, typeof(SomeMessageBConsumer)));

            var aConsumerMock = new Mock<SomeMessageAConsumer>();
            var aConsumer2Mock = new Mock<SomeMessageAConsumer2>();
            var bConsumerMock = new Mock<SomeMessageBConsumer>();
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageAConsumer))).Returns(aConsumerMock.Object);
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageAConsumer2))).Returns(aConsumer2Mock.Object);
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageBConsumer))).Returns(bConsumerMock.Object);

            _providerSettings.EnableMessageSerialization = false;

            var m = new SomeMessageA();

            // act
            _subject.Value.Publish(m).Wait();

            // assert
            aConsumerMock.Verify(x => x.OnHandle(m, topicA), Times.Once);
            aConsumerMock.VerifyNoOtherCalls();
            aConsumer2Mock.Verify(x => x.OnHandle(m, topicA2), Times.Never);
            aConsumer2Mock.VerifyNoOtherCalls();
            bConsumerMock.Verify(x => x.OnHandle(It.IsAny<SomeMessageB>(), topicB), Times.Never);
            bConsumerMock.VerifyNoOtherCalls();
        }

    }

    public class SomeMessageA
    {
    }

    public class SomeMessageB
    {
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
