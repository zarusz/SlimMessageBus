﻿namespace SlimMessageBus.Host.AzureServiceBus.Test;

using System.Collections.Concurrent;
using System.Globalization;

using Azure.Messaging.ServiceBus;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Interceptor;
using SlimMessageBus.Host.Serialization;

public class ServiceBusMessageBusTests : IDisposable
{
    private ServiceBusMessageBusSettings ProviderBusSettings { get; }
    private Lazy<WrappedProviderMessageBus> ProviderBus { get; }
    private MessageBusBuilder BusBuilder { get; } = MessageBusBuilder.Create();

    private IDictionary<string, Mock<ServiceBusSender>> SenderMockByPath { get; } = new ConcurrentDictionary<string, Mock<ServiceBusSender>>();

    public ServiceBusMessageBusTests()
    {
        var messageSerializerProviderMock = new Mock<IMessageSerializerProvider>();
        messageSerializerProviderMock.Setup(x => x.GetSerializer(It.IsAny<string>())).Returns(new Mock<IMessageSerializer>().Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns(Enumerable.Empty<object>());
        serviceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(messageSerializerProviderMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        serviceProviderMock.Setup(x => x.GetService(typeof(IEnumerable<IMessageBusLifecycleInterceptor>))).Returns(Array.Empty<IMessageBusLifecycleInterceptor>());
        serviceProviderMock.Setup(x => x.GetService(typeof(TimeProvider))).Returns(TimeProvider.System);
        serviceProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        serviceProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), TimeProvider.System, NullLoggerFactory.Instance));

        BusBuilder.WithServiceProvider(serviceProviderMock.Object);

        ProviderBusSettings = new ServiceBusMessageBusSettings("connection-string")
        {
            ClientFactory = (_, _) =>
            {
                var client = new Mock<ServiceBusClient>();
                return client.Object;
            },
            SenderFactory = (path, client) =>
            {
                var m = new Mock<ServiceBusSender>();
                SenderMockByPath.Add(path, m);
                return m.Object;
            },
            TopologyProvisioning = new ServiceBusTopologySettings
            {
                Enabled = false
            }
        };
        BusBuilder.WithProvider(mbSettings => new WrappedProviderMessageBus(mbSettings, ProviderBusSettings));
        ProviderBus = new Lazy<WrappedProviderMessageBus>(() => (WrappedProviderMessageBus)BusBuilder.Build());
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (ProviderBus.IsValueCreated)
            {
                ProviderBus.Value.Dispose();
            }
        }
    }

    [Fact]
    public async Task WhenPublishGivenModifierConfiguredForMessageTypeThenModifierExecuted()
    {
        // arrange
        BusBuilder.Produce<SomeMessage>(x =>
        {
            x.DefaultTopic("default-topic");
            x.WithModifier((message, sbMessage) =>
            {
                sbMessage.MessageId = message.Id;
                sbMessage.PartitionKey = (message.Value % 2).ToString(CultureInfo.InvariantCulture);
            });
        });

        var m1 = new SomeMessage { Id = "1", Value = 10 };
        var m2 = new SomeMessage { Id = "2", Value = 3 };

        // act
        await ProviderBus.Value.ProducePublish(m1);
        await ProviderBus.Value.ProducePublish(m2);

        // assert
        var topicClient = SenderMockByPath["default-topic"];
        topicClient.Verify(x => x.SendMessageAsync(It.Is<ServiceBusMessage>(m => m.MessageId == "1" && m.PartitionKey == "0"), It.IsAny<CancellationToken>()), Times.Once);
        topicClient.Verify(x => x.SendMessageAsync(It.Is<ServiceBusMessage>(m => m.MessageId == "2" && m.PartitionKey == "1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_Publish_Given_ModifierConfiguredForMessageTypeThatThrowsError_Then_ModifierDoesNotPreventMessageDelivery()
    {
        // arrange
        BusBuilder.Produce<SomeMessage>(x =>
        {
            x.DefaultTopic("default-topic");
            x.WithModifier((message, sbMessage) => throw new Exception("Someone is having bad day today!"));
        });

        var m = new SomeMessage { Id = "1", Value = 10 };

        // act
        await ProviderBus.Value.ProducePublish(m);

        // assert
        SenderMockByPath["default-topic"].Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void When_Create_Given_SameMessageTypeConfiguredTwiceForTopicAndForQueue_Then_ConfigurationExceptionThrown()
    {
        // arrange
        BusBuilder.Produce<SomeMessage>(x => x.ToTopic());
        BusBuilder.Produce<SomeMessage>(x => x.ToQueue());

        // act
        Func<IMessageBusProvider> creation = () => BusBuilder.Build();

        // assert
        creation.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage($"* {typeof(SomeMessage).FullName} *");
    }

    [Fact]
    public void When_Create_Given_SameDefaultPathUsedForTopicAndForQueue_Then_ConfigurationExceptionThrown()
    {
        // arrange
        const string path = "the-same-name";
        BusBuilder.Produce<SomeMessage>(x => x.DefaultTopic(path));
        BusBuilder.Produce<OtherMessage>(x => x.DefaultQueue(path));

        // act
        Func<IMessageBusProvider> creation = () => BusBuilder.Build();

        // assert
        creation.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage($"The same name '{path}' was used for queue and topic*");
    }

    [Fact]
    public async Task When_Publish_Then_TopicClientOrQueueClientIsCreatedForTopicNameOrQueueName()
    {
        // arrange
        BusBuilder.Produce<SomeMessage>(x => x.DefaultTopic("some-topic"));
        BusBuilder.Produce<OtherMessage>(x => x.DefaultQueue("some-queue"));

        var sm1 = new SomeMessage { Id = "1", Value = 10 };
        var sm2 = new SomeMessage { Id = "2", Value = 12 };
        var om1 = new OtherMessage { Id = "1" };
        var om2 = new OtherMessage { Id = "2" };

        // act
        await ProviderBus.Value.ProducePublish(sm1, "some-topic");
        await ProviderBus.Value.ProducePublish(sm2, "some-topic");
        await ProviderBus.Value.ProducePublish(om1, "some-queue");
        await ProviderBus.Value.ProducePublish(om2, "some-queue");

        // assert
        SenderMockByPath.Should().HaveCount(2);
        SenderMockByPath.Should().ContainKey("some-topic");
        SenderMockByPath.Should().ContainKey("some-queue");
    }
}

public class WrappedProviderMessageBus(
    MessageBusSettings settings,
    ServiceBusMessageBusSettings serviceBusSettings)
    : ServiceBusMessageBus(settings, serviceBusSettings)
{
}

public class SomeMessage
{
    public string Id { get; set; }
    public int Value { get; set; }
}

public class OtherMessage
{
    public string Id { get; set; }
}
