using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.AzureEventHub.Test.Consumer
{
    [TestClass]
    public class ConsumerInstancePoolTest
    {
        private Mock<IDependencyResolver> _dependencyResolverMock;
        private Mock<IMessageSerializer> _serializerMock;
        private Mock<IConsumer<SomeMessage>> _consumerMock;
        private Mock<MessageBusBase> _mbMock;

        [TestInitialize]
        public void Init()
        {
            _consumerMock = new Mock<IConsumer<SomeMessage>>();

            _dependencyResolverMock = new Mock<IDependencyResolver>();
            _dependencyResolverMock.Setup(x => x.Resolve(typeof(IConsumer<SomeMessage>))).Returns(_consumerMock.Object);

            _serializerMock = new Mock<IMessageSerializer>();

            var mbSettings = new MessageBusSettings
            {
                DependencyResolver = _dependencyResolverMock.Object,
                Serializer = _serializerMock.Object
            };

            _mbMock = new Mock<MessageBusBase>(mbSettings);
            _mbMock.SetupGet(x => x.Settings).Returns(mbSettings);
        }

        [TestMethod]
        public void CreatesNInstances()
        {
            // arrange
            var consumerSettings = new ConsumerSettings
            {
                Instances = 2,
                ConsumerMode = ConsumerMode.Subscriber,
                ConsumerType = typeof(IConsumer<SomeMessage>),
                MessageType = typeof(SomeMessage)
            };
                
            // act
            var p = new ConsumerInstancePool<SomeMessage>(consumerSettings, _mbMock.Object, x => new byte[0]);

            // assert
            _dependencyResolverMock.Verify(x => x.Resolve(typeof(IConsumer<SomeMessage>)), Times.Exactly(2));
        }
    }
}
