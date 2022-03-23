namespace SlimMessageBus.Host.Memory.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Newtonsoft.Json;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Interceptor;
    using SlimMessageBus.Host.Serialization;
    using Xunit;

    public class MemoryMessageBusTest
    {
        private readonly Lazy<MemoryMessageBus> _subject;
        private readonly MessageBusSettings _settings = new();
        private readonly MemoryMessageBusSettings _providerSettings = new();
        private readonly Mock<IDependencyResolver> _dependencyResolverMock = new();
        private readonly Mock<IMessageSerializer> _messageSerializerMock = new();

        public MemoryMessageBusTest()
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

            _subject = new Lazy<MemoryMessageBus>(() => new MemoryMessageBus(_settings, _providerSettings));
        }

        private static ProducerSettings Producer(Type messageType, string defaultTopic)
        {
            return new ProducerSettings
            {
                MessageType = messageType,
                DefaultPath = defaultTopic
            };
        }

        private static ConsumerSettings Consumer(Type messageType, string topic, Type consumerType)
        {
            return new ConsumerBuilder<object>(new MessageBusSettings(), messageType).Topic(topic).WithConsumer(consumerType).ConsumerSettings;
        }

        [Fact]
        public void When_Create_Given_MessageSerializationDisabled_And_NoSerializerProvided_Then_NoException()
        {
            // arrange
            _settings.Serializer = null;
            _providerSettings.EnableMessageSerialization = false;

            // act
            Action act = () => { var _ = _subject.Value; };

            // assert            
            act.Should().NotThrow<ConfigurationMessageBusException>();
        }

        [Fact]
        public void When_Create_Given_MessageSerializationEnabled_And_NoSerializerProvided_Then_ThrowsException()
        {
            // arrange
            _settings.Serializer = null;
            _providerSettings.EnableMessageSerialization = true;

            // act
            Action act = () => { var _ = _subject.Value; };

            // assert          
            act.Should().Throw<ConfigurationMessageBusException>();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task When_Publish_Given_MessageSerializationSetting_Then_DeliversMessageInstanceToRespectiveConsumers(bool enableMessageSerialization)
        {
            // arrange
            const string topicA = "topic-a";
            const string topicA2 = "topic-a-2";
            const string topicB = "topic-b";

            _settings.Producers.Add(Producer(typeof(SomeMessageA), topicA));
            _settings.Producers.Add(Producer(typeof(SomeMessageB), topicB));
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
            await _subject.Value.Publish(m);

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

        [Fact]
        public async Task When_Publish_Given_PerMessageScopeEnabled_Then_TheScopeIsCreatedAndConsumerObtainedFromScope()
        {
            // arrange
            var consumerMock = new Mock<SomeMessageAConsumer>();

            var scope = new Mock<IChildDependencyResolver>();
            scope.Setup(x => x.Resolve(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);
            scope.Setup(x => x.Dispose()).Callback(() => { });

            _dependencyResolverMock.Setup(x => x.CreateScope()).Returns(() => scope.Object);

            const string topic = "topic-a";

            _settings.Producers.Add(Producer(typeof(SomeMessageA), topic));
            _settings.Consumers.Add(Consumer(typeof(SomeMessageA), topic, typeof(SomeMessageAConsumer)));
            _settings.IsMessageScopeEnabled = true;

            _providerSettings.EnableMessageSerialization = false;

            var m = new SomeMessageA();

            // act
            await _subject.Value.Publish(m);

            // assert
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(ILoggerFactory)), Times.Once);
            _dependencyResolverMock.Verify(x => x.CreateScope(), Times.Once);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
            _dependencyResolverMock.VerifyNoOtherCalls();

            scope.Verify(x => x.Resolve(typeof(SomeMessageAConsumer)), Times.Once);
            scope.Verify(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
            scope.Verify(x => x.Dispose(), Times.Once);
            scope.VerifyNoOtherCalls();

            consumerMock.Verify(x => x.OnHandle(m, topic), Times.Once);
            consumerMock.Verify(x => x.Dispose(), Times.Never);
            consumerMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task When_Publish_Given_PerMessageScopeDisabled_Then_TheScopeIsNotCreatedAndConsumerObtainedFromRoot()
        {
            // arrange
            var consumerMock = new Mock<SomeMessageAConsumer>();

            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);

            const string topic = "topic-a";

            _settings.Producers.Add(Producer(typeof(SomeMessageA), topic));

            var consumerSettings = Consumer(typeof(SomeMessageA), topic, typeof(SomeMessageAConsumer));
            consumerSettings.IsDisposeConsumerEnabled = true;

            _settings.Consumers.Add(consumerSettings);
            _settings.IsMessageScopeEnabled = false;

            _providerSettings.EnableMessageSerialization = false;

            var m = new SomeMessageA();

            // act
            await _subject.Value.Publish(m);

            // assert
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(ILoggerFactory)), Times.Once);
            _dependencyResolverMock.Verify(x => x.CreateScope(), Times.Never);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(SomeMessageAConsumer)), Times.Once);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
            _dependencyResolverMock.VerifyNoOtherCalls();

            consumerMock.Verify(x => x.OnHandle(m, topic), Times.Once);
            consumerMock.Verify(x => x.Dispose(), Times.Once);
            consumerMock.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(new object[] { true })]
        [InlineData(new object[] { false })]
        public async Task When_Publish_Given_PerMessageScopeDisabledOrEnabled_And_OutterBusCreatedMesssageScope_Then_TheScopeIsNotCreated_And_ConsumerObtainedFromCurrentMessageScope(bool isMessageScopeEnabled)
        {
            // arrange
            var consumerMock = new Mock<SomeMessageAConsumer>();

            _dependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);

            var currentScopeDependencyResolverMock = new Mock<IDependencyResolver>();
            currentScopeDependencyResolverMock.Setup(x => x.Resolve(typeof(SomeMessageAConsumer))).Returns(() => consumerMock.Object);

            const string topic = "topic-a";

            _settings.Producers.Add(Producer(typeof(SomeMessageA), topic));

            var consumerSettings = Consumer(typeof(SomeMessageA), topic, typeof(SomeMessageAConsumer));
            consumerSettings.IsDisposeConsumerEnabled = true;

            _settings.Consumers.Add(consumerSettings);
            _settings.IsMessageScopeEnabled = isMessageScopeEnabled;

            _providerSettings.EnableMessageSerialization = false;

            var m = new SomeMessageA();

            // set current scope
            MessageScope.Current = currentScopeDependencyResolverMock.Object;

            // act
            await _subject.Value.Publish(m);

            // assert

            // current scope is not changed
            MessageScope.Current.Should().BeSameAs(currentScopeDependencyResolverMock.Object);

            _dependencyResolverMock.Verify(x => x.Resolve(typeof(ILoggerFactory)), Times.Once);
            _dependencyResolverMock.Verify(x => x.CreateScope(), Times.Never);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(SomeMessageAConsumer)), Times.Never);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IProducerInterceptor<SomeMessageA>>)), Times.Once);
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IPublishInterceptor<SomeMessageA>>)), Times.Once);
            _dependencyResolverMock.VerifyNoOtherCalls();

            currentScopeDependencyResolverMock.Verify(x => x.CreateScope(), Times.Never);
            currentScopeDependencyResolverMock.Verify(x => x.Resolve(typeof(SomeMessageAConsumer)), Times.Once);
            currentScopeDependencyResolverMock.Verify(x => x.Resolve(typeof(IEnumerable<IConsumerInterceptor<SomeMessageA>>)), Times.Once);
            currentScopeDependencyResolverMock.VerifyNoOtherCalls();

            consumerMock.Verify(x => x.OnHandle(m, topic), Times.Once);
            consumerMock.Verify(x => x.Dispose(), Times.Once);
            consumerMock.VerifyNoOtherCalls();
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
