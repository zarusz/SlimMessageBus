using Moq;
using Newtonsoft.Json;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SlimMessageBus.Host.Hybrid.Test
{
    public class HybridMessageBusTest
    {
        private readonly Lazy<HybridMessageBus> _subject;
        private readonly MessageBusSettings _settings = new MessageBusSettings();
        private readonly HybridMessageBusSettings _providerSettings = new HybridMessageBusSettings();
        private readonly Mock<IDependencyResolver> _dependencyResolverMock = new Mock<IDependencyResolver>();
        private readonly Mock<IMessageSerializer> _messageSerializerMock = new Mock<IMessageSerializer>();

        public HybridMessageBusTest()
        {
            _settings.DependencyResolver = _dependencyResolverMock.Object;
            _settings.Serializer = _messageSerializerMock.Object;

            _messageSerializerMock
                .Setup(x => x.Serialize(It.IsAny<Type>(), It.IsAny<object>()))
                .Returns((Type type, object message) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)));
            _messageSerializerMock
                .Setup(x => x.Deserialize(It.IsAny<Type>(), It.IsAny<byte[]>()))
                .Returns((Type type, byte[] payload) => JsonConvert.DeserializeObject(Encoding.UTF8.GetString(payload), type));

            _subject = new Lazy<HybridMessageBus>(() => new HybridMessageBus(_settings, _providerSettings));
        }

        [Fact]
        public async Task When_Send_Then_RoutesToProperBus()
        {
            // arrange
            Mock<MessageBusBase> bus1Mock = null;
            Mock<MessageBusBase> bus2Mock = null;

            _providerSettings["bus1"] = (mbb) =>
             {
                 mbb.Produce<SomeMessage>(x => x.DefaultTopic("topic1")).WithProvider(mbs =>
                 {
                     bus1Mock = new Mock<MessageBusBase>(new[] { mbs });
                     bus1Mock.SetupGet(x => x.Settings).Returns(mbs);
                     bus1Mock.Setup(x => x.Publish(It.IsAny<SomeMessage>(), It.IsAny<string>())).Returns(Task.CompletedTask);

                     return bus1Mock.Object;
                 });
             };
            _providerSettings["bus2"] = (mbb) =>
            {
                mbb.Produce<SomeRequest>(x => x.DefaultTopic("topic2")).WithProvider(mbs =>
                {
                    bus2Mock = new Mock<MessageBusBase>(new[] { mbs });
                    bus2Mock.SetupGet(x => x.Settings).Returns(mbs);
                    bus2Mock.Setup(x => x.Send(It.IsAny<SomeRequest>(), It.IsAny<string>(), default)).Returns(Task.FromResult(new SomeResponse()));

                    return bus2Mock.Object;
                });
            };

            var someMessage = new SomeMessage();
            var someDerivedMessage = new SomeDerivedMessage();
            var someRequest = new SomeRequest();
            var someDerivedRequest = new SomeDerivedRequest();

            // act
            await _subject.Value.Publish(someMessage);
            await _subject.Value.Publish(someDerivedMessage);
            await _subject.Value.Send(someRequest);
            await _subject.Value.Send(someDerivedRequest);

            // assert
            bus1Mock.Verify(x => x.Publish(someMessage, null), Times.Once);
            bus1Mock.Verify(x => x.Publish<SomeMessage>(someDerivedMessage, null), Times.Once);
            bus2Mock.Verify(x => x.Send(someRequest, null, default), Times.Once);
            bus2Mock.Verify(x => x.Send(someDerivedRequest, null, default), Times.Once);
        }

        public class SomeMessage
        {
        }

        public class SomeDerivedMessage : SomeMessage
        {
        }

        public class SomeRequest : IRequestMessage<SomeResponse>
        {
        }

        public class SomeDerivedRequest : SomeRequest
        {
        }

        public class SomeResponse
        {
        }
    }


}
