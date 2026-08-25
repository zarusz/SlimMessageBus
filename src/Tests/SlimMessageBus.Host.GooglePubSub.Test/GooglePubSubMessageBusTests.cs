namespace SlimMessageBus.Host.GooglePubSub.Test;

public class GooglePubSubMessageBusTests
{
    private record TestMessage(string Id);

    [Fact]
    public async Task Publish_UsesTopicClientHeadersPayloadAndModifier()
    {
        var publisher = new Mock<PublisherClient>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<PubsubMessage>())).ReturnsAsync("message-id");

        var serializer = new Mock<IMessageSerializer>();
        serializer.Setup(x => x.Serialize(typeof(TestMessage), It.IsAny<IDictionary<string, object>>(), It.IsAny<object>(), It.IsAny<object>()))
            .Returns([1, 2, 3]);
        var serializerProvider = new Mock<IMessageSerializerProvider>();
        serializerProvider.Setup(x => x.GetSerializer("orders")).Returns(serializer.Object);

        var serviceProvider = CreateServiceProvider(serializerProvider.Object);
        var builder = MessageBusBuilder.Create().WithServiceProvider(serviceProvider);
        builder.Produce<TestMessage>(x => x
            .DefaultTopic("orders")
            .WithModifier((message, transportMessage) => transportMessage.OrderingKey = message.Id));

        var settings = new GooglePubSubMessageBusSettings
        {
            ProjectId = "project",
            TopologyProvisioning = new GooglePubSubTopologySettings { Enabled = false },
            PublisherClientFactory = (_, topic, _) =>
            {
                topic.ToString().Should().Be("projects/project/topics/orders");
                return Task.FromResult(publisher.Object);
            }
        };
        builder.WithProvider(messageBusSettings => new GooglePubSubMessageBus(messageBusSettings, settings));
        await using var bus = (GooglePubSubMessageBus)builder.Build();

        await bus.ProducePublish(new TestMessage("customer-1"), headers: new Dictionary<string, object> { ["attempt"] = 3 });

        publisher.Verify(x => x.PublishAsync(It.Is<PubsubMessage>(message =>
            message.Data.ToByteArray().SequenceEqual(new byte[] { 1, 2, 3 }) &&
            message.OrderingKey == "customer-1" &&
            message.Attributes["attempt"] == "smb:int:3")), Times.Once);
    }

    private static IServiceProvider CreateServiceProvider(IMessageSerializerProvider serializerProvider)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(It.Is<Type>(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
            .Returns(Enumerable.Empty<object>());
        serviceProvider.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(serializerProvider);
        serviceProvider.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        serviceProvider.Setup(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>))).Returns(Array.Empty<IMessageBusLifecycleInterceptor>());
        serviceProvider.Setup(x => x.GetService(typeof(TimeProvider))).Returns(TimeProvider.System);
        serviceProvider.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        serviceProvider.Setup(x => x.GetService(typeof(IPendingRequestManager)))
            .Returns(new PendingRequestManager(new InMemoryPendingRequestStore(), TimeProvider.System, NullLoggerFactory.Instance));
        return serviceProvider.Object;
    }
}
