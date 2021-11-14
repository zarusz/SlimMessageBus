namespace SlimMessageBus.Host.Test
{
    using System;
    using System.Collections.Generic;
    using Moq;
    using SlimMessageBus.Host.Config;
    using SlimMessageBus.Host.DependencyResolver;
    using SlimMessageBus.Host.Serialization;

    public class MessageBusMock
    {
        public Mock<IDependencyResolver> DependencyResolverMock { get; }
        public IList<Mock<IChildDependencyResolver>> ChildDependencyResolverMocks { get; }
        public Action<Mock<IChildDependencyResolver>> OnChildDependencyResolverCreated { get; set; }
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

            ChildDependencyResolverMocks = new List<Mock<IChildDependencyResolver>>();

            void SetupDependencyResolver<T>(Mock<T> mock) where T : class, IDependencyResolver
            {
                mock.Setup(x => x.Resolve(typeof(IConsumer<SomeMessage>))).Returns(ConsumerMock.Object);
                mock.Setup(x => x.Resolve(typeof(IRequestHandler<SomeRequest, SomeResponse>))).Returns(HandlerMock.Object);
            }

            DependencyResolverMock = new Mock<IDependencyResolver>();
            SetupDependencyResolver(DependencyResolverMock);
            DependencyResolverMock.Setup(x => x.CreateScope()).Returns(() =>
            {
                var mock = new Mock<IChildDependencyResolver>();
                SetupDependencyResolver(mock);

                ChildDependencyResolverMocks.Add(mock);

                OnChildDependencyResolverCreated?.Invoke(mock);

                mock.Setup(x => x.Dispose()).Callback(() =>
                {
                    ChildDependencyResolverMocks.Remove(mock);
                });

                return mock.Object;
            });

            SerializerMock = new Mock<IMessageSerializer>();

            var mbSettings = new MessageBusSettings
            {
                DependencyResolver = DependencyResolverMock.Object,
                Serializer = SerializerMock.Object
            };

            CurrentTime = DateTimeOffset.UtcNow;

            BusMock = new Mock<MessageBusBase>(mbSettings);
            BusMock.SetupGet(x => x.Settings).Returns(mbSettings);
            BusMock.SetupGet(x => x.Serializer).CallBase();
            BusMock.SetupGet(x => x.CurrentTime).Returns(() => CurrentTime);
            BusMock.Setup(x => x.CreateHeaders()).CallBase();
        }
    }
}