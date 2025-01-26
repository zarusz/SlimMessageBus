namespace SlimMessageBus.Host.Test;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Serialization;

public class MessageBusMock : ICurrentTimeProvider
{
    public Mock<IServiceProvider> ServiceProviderMock { get; }
    public IList<Mock<IServiceScope>> ChildDependencyResolverMocks { get; }
    public Action<Mock<IServiceScope>, Mock<IServiceProvider>> OnChildDependencyResolverCreated { get; set; }
    public Mock<IMessageSerializer> SerializerMock { get; }
    public Mock<IConsumer<SomeMessage>> ConsumerMock { get; }
    public Mock<IRequestHandler<SomeRequest, SomeResponse>> HandlerMock { get; }
    public DateTimeOffset CurrentTime { get; set; }
    public Mock<MessageBusBase> BusMock { get; }
    public MessageBusBase Bus => BusMock.Object;

    private static readonly Type[] InterceptorTypes = [typeof(IConsumerInterceptor<>), typeof(IRequestHandlerInterceptor<,>)];

    public MessageBusMock()
    {
        ConsumerMock = new Mock<IConsumer<SomeMessage>>();
        HandlerMock = new Mock<IRequestHandler<SomeRequest, SomeResponse>>();

        ChildDependencyResolverMocks = [];

        var currentTimeProviderMock = new Mock<ICurrentTimeProvider>();
        currentTimeProviderMock.SetupGet(x => x.CurrentTime).Returns(() => CurrentTime);

        void SetupDependencyResolver<T>(Mock<T> mock) where T : class, IServiceProvider
        {
            mock.Setup(x => x.GetService(typeof(IConsumer<SomeMessage>))).Returns(ConsumerMock.Object);
            mock.Setup(x => x.GetService(typeof(IRequestHandler<SomeRequest, SomeResponse>))).Returns(HandlerMock.Object);
            mock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
            mock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>) && t.GetGenericArguments().Length == 1 && t.GetGenericArguments()[0].IsGenericType && InterceptorTypes.Contains(t.GetGenericArguments()[0].GetGenericTypeDefinition()))))
                .Returns(Enumerable.Empty<object>());
            mock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
            mock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), currentTimeProviderMock.Object, NullLoggerFactory.Instance));
        }

        ServiceProviderMock = new Mock<IServiceProvider>();
        SetupDependencyResolver(ServiceProviderMock);

        var serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        serviceScopeFactoryMock.Setup(x => x.CreateScope()).Returns(() =>
        {
            var svpMock = new Mock<IServiceProvider>();
            SetupDependencyResolver(svpMock);

            var mock = new Mock<IServiceScope>();
            mock.SetupGet(x => x.ServiceProvider).Returns(svpMock.Object);

            ChildDependencyResolverMocks.Add(mock);

            OnChildDependencyResolverCreated?.Invoke(mock, svpMock);

            mock.Setup(x => x.Dispose()).Callback(() =>
            {
                ChildDependencyResolverMocks.Remove(mock);
            });

            return mock.Object;
        });

        SerializerMock = new Mock<IMessageSerializer>();

        ServiceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(serviceScopeFactoryMock.Object);
        ServiceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializer))).Returns(SerializerMock.Object);
        ServiceProviderMock.Setup(x => x.GetService(typeof(ICurrentTimeProvider))).Returns(this);

        var mbSettings = new MessageBusSettings
        {
            ServiceProvider = ServiceProviderMock.Object
        };

        CurrentTime = DateTimeOffset.UtcNow;

        BusMock = new Mock<MessageBusBase>(mbSettings);
        BusMock.SetupGet(x => x.Settings).Returns(mbSettings);
        BusMock.SetupGet(x => x.Serializer).CallBase();
        BusMock.SetupGet(x => x.MessageBusTarget).CallBase();
        BusMock.Setup(x => x.CreateHeaders()).CallBase();
        BusMock.Setup(x => x.CreateMessageScope(It.IsAny<ConsumerSettings>(), It.IsAny<object>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<IServiceProvider>())).CallBase();
    }
}