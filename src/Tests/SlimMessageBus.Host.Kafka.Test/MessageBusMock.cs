namespace SlimMessageBus.Host.Kafka.Test;

using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Test.Common;

public class MessageBusMock
{
    public ServiceProviderMock ServiceProviderMock { get; }
    public Mock<IMessageSerializer> SerializerMock { get; }
    public MessageBusSettings BusSettings { get; }

    public CurrentTimeProviderFake CurrentTimeProvider { get; set; }
    public Mock<MessageBusBase> BusMock { get; }
    public MessageBusBase Bus => BusMock.Object;

    public MessageBusMock()
    {
        CurrentTimeProvider = new CurrentTimeProviderFake
        {
            CurrentTime = DateTimeOffset.UtcNow
        };

        SerializerMock = new Mock<IMessageSerializer>();

        ServiceProviderMock = new ServiceProviderMock();
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageSerializer))).Returns(SerializerMock.Object);
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(ICurrentTimeProvider))).Returns(CurrentTimeProvider);

        BusSettings = new MessageBusSettings
        {
            ServiceProvider = ServiceProviderMock.ProviderMock.Object,
        };

        BusMock = new Mock<MessageBusBase>(BusSettings);
        BusMock.SetupGet(x => x.Settings).Returns(BusSettings);
    }
}

