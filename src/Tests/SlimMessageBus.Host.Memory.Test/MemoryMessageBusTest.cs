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
        private readonly Mock<IDependencyResolver> _dependencyResolverMock =new Mock<IDependencyResolver>();
        private readonly Mock<IMessageSerializer> _messageSerializerMock = new Mock<IMessageSerializer>();

        public MemoryMessageBusTest()
        {
            _settings.DependencyResolver = _dependencyResolverMock.Object;
            _settings.Serializer = _messageSerializerMock.Object;

            _subject = new Lazy<MemoryMessageBus>(() => new MemoryMessageBus(_settings, _providerSettings));
        }

        public class SomeMessage
        {
        }

        public class SomeMessageB
        {
        }


        public class SomeMessageConsumer : IConsumer<SomeMessage>
        {
            #region Implementation of IConsumer<in SomeMessage>

            public virtual Task OnHandle(SomeMessage message, string topic)
            {
                return Task.CompletedTask;
            }

            #endregion
        }

        public class SomeMessageConsumer2 : IConsumer<SomeMessage>
        {
            #region Implementation of IConsumer<in SomeMessage>

            public virtual Task OnHandle(SomeMessage message, string topic)
            {
                return Task.CompletedTask;
            }

            #endregion
        }

        [Fact]
        public void Publish_DeliversMessagesToRespectiveConsumers()
        {
            // arrange
            var topic = "topic";
            var topic2 = "topic-2";

            _settings.Publishers.Add(new PublisherSettings {MessageType = typeof(SomeMessage), DefaultTopic = topic});
            _settings.Consumers.Add(new ConsumerSettings
            {
                MessageType = typeof(SomeMessage),
                Topic = topic,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(SomeMessageConsumer)
            });
            _settings.Consumers.Add(new ConsumerSettings
            {
                MessageType = typeof(SomeMessage),
                Topic = topic,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(SomeMessageConsumer2)
            });
            _settings.Consumers.Add(new ConsumerSettings
            {
                MessageType = typeof(SomeMessage),
                Topic = topic2,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(SomeMessageConsumer)
            });

            var consumerMock = new Mock<SomeMessageConsumer>();
            var consumer2Mock = new Mock<SomeMessageConsumer2>();
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageConsumer))).Returns(consumerMock.Object);
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageConsumer2))).Returns(consumer2Mock.Object);

            _providerSettings.EnableMessageSerialization = false;

            var m = new SomeMessage();

            // act
            _subject.Value.Publish(m).Wait();

            // assert
            consumerMock.Verify(x => x.OnHandle(m, topic), Times.Once);
            consumer2Mock.Verify(x => x.OnHandle(m, topic), Times.Once);
            consumerMock.Verify(x => x.OnHandle(m, topic2), Times.Never);
        }
    }
}
