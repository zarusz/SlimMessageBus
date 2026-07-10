namespace SlimMessageBus.Host.AmazonSQS.Test;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Logging.Abstractions;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Serialization;

public class SqsMessageBusTest : IDisposable
{
    private readonly Mock<IAmazonSQS> _sqsClientMock;
    private readonly Mock<IAmazonSimpleNotificationService> _snsClientMock;
    private readonly SqsMessageBus _subject;

    public SqsMessageBusTest()
    {
        _sqsClientMock = new Mock<IAmazonSQS>();
        _snsClientMock = new Mock<IAmazonSimpleNotificationService>();

        var sqsClientProviderMock = new Mock<ISqsClientProvider>();
        sqsClientProviderMock.SetupGet(x => x.Client).Returns(_sqsClientMock.Object);
        sqsClientProviderMock.Setup(x => x.EnsureClientAuthenticated()).Returns(Task.CompletedTask);

        var snsClientProviderMock = new Mock<ISnsClientProvider>();
        snsClientProviderMock.SetupGet(x => x.Client).Returns(_snsClientMock.Object);
        snsClientProviderMock.Setup(x => x.EnsureClientAuthenticated()).Returns(Task.CompletedTask);

        var messageSerializerProviderMock = new Mock<IMessageSerializerProvider>();
        messageSerializerProviderMock
            .Setup(x => x.GetSerializer(It.IsAny<string>()))
            .Returns(new TestMessageSerializer());

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ISqsClientProvider))).Returns(sqsClientProviderMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ISnsClientProvider))).Returns(snsClientProviderMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IMessageSerializerProvider))).Returns(messageSerializerProviderMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IMessageTypeResolver))).Returns(new AssemblyQualifiedNameMessageTypeResolver());
        serviceProviderMock.Setup(x => x.GetService(typeof(TimeProvider))).Returns(TimeProvider.System);
        serviceProviderMock.Setup(x => x.GetService(typeof(RuntimeTypeCache))).Returns(new RuntimeTypeCache());
        serviceProviderMock.Setup(x => x.GetService(typeof(IPendingRequestManager))).Returns(() => new PendingRequestManager(new InMemoryPendingRequestStore(), TimeProvider.System, NullLoggerFactory.Instance));
        serviceProviderMock.Setup(x => x.GetService(It.Is<Type>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)))).Returns((Type t) => Array.CreateInstance(t.GetGenericArguments()[0], 0));

        var messageBusSettings = new MessageBusSettings
        {
            ServiceProvider = serviceProviderMock.Object
        };

        var producerSettings = new ProducerSettings();
        new ProducerBuilder<TopicMessage>(producerSettings).DefaultTopic("test-topic");
        messageBusSettings.Producers.Add(producerSettings);

        _subject = new SqsMessageBus(messageBusSettings, new SqsMessageBusSettings
        {
            TopologyProvisioning = new SqsTopologySettings
            {
                Enabled = false
            }
        });
    }

    public void Dispose()
    {
        _subject.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task When_Publish_Given_TopicProducerAndProvisioningDisabled_Then_LooksUpTopicAndPublishesToSns()
    {
        // arrange
        const string topicName = "test-topic";
        const string topicArn = "arn:aws:sns:eu-central-1:123456789012:test-topic";

        _snsClientMock.Setup(x => x.FindTopicAsync(topicName))
            .ReturnsAsync(new Topic { TopicArn = topicArn });
        _snsClientMock.Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse());

        // act
        await _subject.ProducePublish(new TopicMessage());

        // assert
        _snsClientMock.Verify(x => x.FindTopicAsync(topicName), Times.Once);
        _snsClientMock.Verify(x => x.PublishAsync(
            It.Is<PublishRequest>(r => r.TopicArn == topicArn),
            It.IsAny<CancellationToken>()), Times.Once);

        _sqsClientMock.Verify(x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sqsClientMock.Verify(x => x.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private record TopicMessage;

    private class TestMessageSerializer : IMessageSerializer, IMessageSerializer<string>
    {
        byte[] IMessageSerializer<byte[]>.Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
            => [];

        object IMessageSerializer<byte[]>.Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, byte[] payload, object transportMessage)
            => throw new NotSupportedException();

        string IMessageSerializer<string>.Serialize(Type messageType, IDictionary<string, object> headers, object message, object transportMessage)
            => "{}";

        object IMessageSerializer<string>.Deserialize(Type messageType, IReadOnlyDictionary<string, object> headers, string payload, object transportMessage)
            => throw new NotSupportedException();
    }
}
