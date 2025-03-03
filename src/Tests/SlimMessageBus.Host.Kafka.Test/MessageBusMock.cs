namespace SlimMessageBus.Host.Kafka.Test;

public class MessageBusMock
{
    public ServiceProviderMock ServiceProviderMock { get; }
    public Mock<IMessageSerializer> SerializerMock { get; }
    public MessageBusSettings BusSettings { get; }
    public FakeTimeProvider TimeProvider { get; set; }
    public Mock<MessageBusBase> BusMock { get; }
    public MessageBusBase Bus => BusMock.Object;

    public MessageBusMock()
    {
        TimeProvider = new FakeTimeProvider();

        SerializerMock = new Mock<IMessageSerializer>();

        ServiceProviderMock = new ServiceProviderMock();
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageSerializer))).Returns(SerializerMock.Object);
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(TimeProvider))).Returns(TimeProvider);
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Array.CreateInstance(t.GetGenericArguments()[0], 0));
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        ServiceProviderMock.ProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), TimeProvider, NullLoggerFactory.Instance));

        BusSettings = new MessageBusSettings
        {
            ServiceProvider = ServiceProviderMock.ProviderMock.Object,
        };

        BusMock = new Mock<MessageBusBase>(BusSettings);
        BusMock.SetupGet(x => x.Settings).Returns(BusSettings);
    }
}