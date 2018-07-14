using System;
using Moq;
using SlimMessageBus.Host.Config;

namespace SlimMessageBus.Host.Test
{
    public class MessageBusMock
    {
        public Mock<IDependencyResolver> DependencyResolverMock { get; }
        public Mock<IMessageSerializer> SerializerMock { get; }
        public Mock<IConsumer<SomeMessage>> ConsumerMock { get; }
        public Mock<IRequestHandler<SomeRequest, SomeResponse>> HandlerMock { get; }
        public DateTimeOffset CurrentTime { get; set; }
        public Mock<MessageBusBase> BusMock { get; }
        public MessageBusBase Bus => BusMock.Object;

        public MessageBusMock()
        {
            ConsumerMock = new Mock<IConsumer<SomeMessage>>();
            HandlerMock = new Mock<IRequestHandler<SomeRequest, SomeResponse>>();

            DependencyResolverMock = new Mock<IDependencyResolver>();
            DependencyResolverMock.Setup(x => x.Resolve(typeof(IConsumer<SomeMessage>))).Returns(ConsumerMock.Object);
            DependencyResolverMock.Setup(x => x.Resolve(typeof(IRequestHandler<SomeRequest, SomeResponse>))).Returns(HandlerMock.Object);

            SerializerMock = new Mock<IMessageSerializer>();

            var mbSettings = new MessageBusSettings
            {
                DependencyResolver = DependencyResolverMock.Object,
                Serializer = SerializerMock.Object
            };

            CurrentTime = DateTimeOffset.UtcNow;

            BusMock = new Mock<MessageBusBase>(mbSettings);
            BusMock.SetupGet(x => x.Settings).Returns(mbSettings);
            BusMock.SetupGet(x => x.CurrentTime).Returns(() => CurrentTime);
        }
    }
}