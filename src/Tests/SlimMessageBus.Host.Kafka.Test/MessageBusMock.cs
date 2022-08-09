namespace SlimMessageBus.Host.Kafka.Test;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Serialization;

public class MessageBusMock
{
    public Mock<IDependencyResolver> DependencyResolverMock { get; }
    public Mock<IMessageSerializer> SerializerMock { get; }
    public MessageBusSettings BusSettings { get; }

    public DateTimeOffset CurrentTime { get; set; }
    public Mock<MessageBusBase> BusMock { get; }
    public MessageBusBase Bus => BusMock.Object;

    public MessageBusMock()
    {
        DependencyResolverMock = new Mock<IDependencyResolver>();

        SerializerMock = new Mock<IMessageSerializer>();

        BusSettings = new MessageBusSettings
        {
            DependencyResolver = DependencyResolverMock.Object,
            Serializer = SerializerMock.Object
        };

        CurrentTime = DateTimeOffset.UtcNow;

        BusMock = new Mock<MessageBusBase>(BusSettings);
        BusMock.SetupGet(x => x.Settings).Returns(BusSettings);
        BusMock.SetupGet(x => x.CurrentTime).Returns(() => CurrentTime);
    }
}

