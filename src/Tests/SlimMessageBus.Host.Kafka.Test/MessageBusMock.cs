namespace SlimMessageBus.Host.Kafka.Test;

using SlimMessageBus.Host.Collections;
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
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Array.CreateInstance(t.GetGenericArguments()[0], 0));
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), new CurrentTimeProvider(), NullLoggerFactory.Instance));

        BusSettings = new MessageBusSettings
        {
            ServiceProvider = ServiceProviderMock.ProviderMock.Object,
        };

        BusMock = new Mock<MessageBusBase>(BusSettings);
        BusMock.SetupGet(x => x.Settings).Returns(BusSettings);
    }
}

