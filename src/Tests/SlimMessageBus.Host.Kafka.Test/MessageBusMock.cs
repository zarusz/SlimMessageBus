namespace SlimMessageBus.Host.Kafka.Test;

using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Test.Common;

public class MessageBusMock
{
    public ServiceProviderMock ServiceProviderMock { get; }
    public Mock<IMessageSerializer> SerializerMock { get; }
    public MessageBusSettings BusSettings { get; }

    public DateTimeOffset CurrentTime { get; set; }
    public Mock<MessageBusBase> BusMock { get; }
    public MessageBusBase Bus => BusMock.Object;

    public MessageBusMock()
    {
        ServiceProviderMock = new ServiceProviderMock();

        SerializerMock = new Mock<IMessageSerializer>();

        BusSettings = new MessageBusSettings
        {
            ServiceProvider = ServiceProviderMock.ProviderMock.Object,
            Serializer = SerializerMock.Object
        };

        CurrentTime = DateTimeOffset.UtcNow;

        BusMock = new Mock<MessageBusBase>(BusSettings);
        BusMock.SetupGet(x => x.Settings).Returns(BusSettings);
        BusMock.SetupGet(x => x.CurrentTime).Returns(() => CurrentTime);
    }
}

