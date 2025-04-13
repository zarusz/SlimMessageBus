namespace SlimMessageBus.Host.AmazonSQS.Test;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

using Newtonsoft.Json;

using SlimMessageBus.Host;
using SlimMessageBus.Host.AmazonSQS;

public class SqsTopologyServiceTest
{
    private readonly Mock<ILogger<SqsTopologyService>> _loggerMock;
    private readonly MessageBusSettings _messageBusSettings;
    private readonly SqsMessageBusSettings _providerSettings;
    private readonly Mock<ISqsClientProvider> _sqsClientProviderMock;
    private readonly Mock<ISnsClientProvider> _snsClientProviderMock;
    private readonly Mock<ISqsTopologyCache> _cacheMock;
    private readonly Mock<IAmazonSQS> _sqsClientMock;
    private readonly Mock<IAmazonSimpleNotificationService> _snsClientMock;
    private readonly SqsTopologySettings _topologySettings;

    private readonly SqsTopologyService _subject;

    public SqsTopologyServiceTest()
    {
        _loggerMock = new Mock<ILogger<SqsTopologyService>>();

        _sqsClientProviderMock = new Mock<ISqsClientProvider>();
        _snsClientProviderMock = new Mock<ISnsClientProvider>();
        _cacheMock = new Mock<ISqsTopologyCache>();
        _sqsClientMock = new Mock<IAmazonSQS>();
        _snsClientMock = new Mock<IAmazonSimpleNotificationService>();

        _sqsClientProviderMock.Setup(x => x.Client).Returns(_sqsClientMock.Object);
        _snsClientProviderMock.Setup(x => x.Client).Returns(_snsClientMock.Object);

        _topologySettings = new SqsTopologySettings
        {
            CanConsumerCreateQueue = true,
            CanConsumerCreateTopic = true,
            CanConsumerCreateTopicSubscription = true,
            CanProducerCreateQueue = true,
            CanProducerCreateTopic = true,
            Enabled = true,
            OnProvisionTopology = (sqs, sns, action, ct) => action()
        };

        _messageBusSettings = new MessageBusSettings();
        _providerSettings = new SqsMessageBusSettings
        {
            TopologyProvisioning = _topologySettings
        };

        _subject = new SqsTopologyService(
            _loggerMock.Object,
            _messageBusSettings,
            _providerSettings,
            _sqsClientProviderMock.Object,
            _snsClientProviderMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task When_ProvisionTopology_Then_OnProvisionTopologyIsCalled_Given_ValidParameters()
    {
        // Arrange
        var provisionCalled = false;
        _topologySettings.OnProvisionTopology = (sqs, sns, action, ct) =>
        {
            provisionCalled = true;
            sqs.Should().Be(_sqsClientMock.Object);
            sns.Should().Be(_snsClientMock.Object);
            return Task.CompletedTask;
        };

        // Act
        await _subject.ProvisionTopology(CancellationToken.None);

        // Assert
        provisionCalled.Should().BeTrue();
    }

    [Fact]
    public void When_GetQueuePolicyForTopic_Then_ReturnsValidJsonPolicy_Given_QueueAndTopicArns()
    {
        // Arrange
        var queueArn = "arn:aws:sqs:us-east-1:123456789012:my-queue";
        var topicArn = "arn:aws:sns:us-east-1:123456789012:my-topic";

        // Act
        var result = InvokeGetQueuePolicyForTopic(_subject, queueArn, topicArn);

        // Assert
        result.Should().NotBeNullOrEmpty();

        var policy = JsonConvert.DeserializeObject<dynamic>(result);
        ((string)policy.Version).Should().Be("2012-10-17");
        ((string)policy.Statement[0].Effect).Should().Be("Allow");
        ((string)policy.Statement[0].Resource).Should().Be(queueArn);
        ((string)policy.Statement[0].Condition.ArnEquals["aws:SourceArn"]).Should().Be(topicArn);
    }

    [Fact]
    public async Task When_EnsureQueue_Then_NoActionIsTaken_Given_QueueExistsInCache()
    {
        // Arrange
        var queueName = "test-queue";
        _cacheMock.Setup(x => x.Contains(queueName)).Returns(true);

        // Act
        await InvokeEnsureQueue(_subject, queueName, false, null, null, [], [], true, CancellationToken.None);

        // Assert
        _sqsClientMock.Verify(x => x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.LookupQueue(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_EnsureQueue_Then_LookupIsCalledAndMetaIsSet_Given_QueueIsNotInCache()
    {
        // Arrange
        var queueName = "test-queue";
        _cacheMock.Setup(x => x.Contains(queueName)).Returns(false);
        var queueMeta = new SqsPathMeta("https://sqs.us-east-1.amazonaws.com/123456789012/test-queue", "arn:aws:sqs:us-east-1:123456789012:test-queue", PathKind.Queue);
        _cacheMock.Setup(x => x.LookupQueue(queueName, It.IsAny<CancellationToken>())).ReturnsAsync(queueMeta);

        // Act
        await InvokeEnsureQueue(_subject, queueName, false, null, null, [], [], true, CancellationToken.None);

        // Assert
        _cacheMock.Verify(x => x.LookupQueue(queueName, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.SetMeta(queueName, queueMeta), Times.Once);
        _sqsClientMock.Verify(x => x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_EnsureQueue_Then_QueueIsCreated_Given_QueueDoesNotExistAndCanCreate()
    {
        // Arrange
        var queueName = "test-queue";
        _cacheMock.Setup(x => x.Contains(queueName)).Returns(false);
        _cacheMock.Setup(x => x.LookupQueue(queueName, It.IsAny<CancellationToken>())).ReturnsAsync((SqsPathMeta)null);

        var createQueueResponse = new CreateQueueResponse { QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue" };
        _sqsClientMock.Setup(x => x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createQueueResponse);

        var getQueueAttributesResponse = new GetQueueAttributesResponse
        {
            Attributes = { { QueueAttributeName.QueueArn, "arn:aws:sqs:us-east-1:123456789012:test-queue" } }
        };
        _sqsClientMock.Setup(x => x.GetQueueAttributesAsync(createQueueResponse.QueueUrl, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(getQueueAttributesResponse);

        // Act
        await InvokeEnsureQueue(_subject, queueName, false, 30, null, [], [], true, CancellationToken.None);

        // Assert
        _sqsClientMock.Verify(x => x.CreateQueueAsync(
            It.Is<CreateQueueRequest>(req => req.QueueName == queueName && req.Attributes.ContainsKey(QueueAttributeName.VisibilityTimeout)),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.SetMeta(
            queueName,
            It.Is<SqsPathMeta>(m => m.Url == createQueueResponse.QueueUrl && m.Arn == getQueueAttributesResponse.QueueARN)),
            Times.Once);
    }

    [Fact]
    public async Task When_EnsureQueue_Then_LogsWarning_Given_CannotCreate()
    {
        // Arrange
        var queueName = "test-queue";
        _cacheMock.Setup(x => x.Contains(queueName)).Returns(false);
        _cacheMock.Setup(x => x.LookupQueue(queueName, It.IsAny<CancellationToken>())).ReturnsAsync((SqsPathMeta)null);

        // Act
        await InvokeEnsureQueue(_subject, queueName, false, null, null, [], [], false, CancellationToken.None);

        // Assert
        _sqsClientMock.Verify(x => x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        //_loggerMock.Verify(logger => logger.LogWarning("Cannot create queue {QueueName} as the provider does not allow it", queueName));
    }

    [Fact]
    public async Task When_EnsureTopic_Then_NoActionIsTaken_Given_TopicExistsInCache()
    {
        // Arrange
        var topicName = "test-topic";
        _cacheMock.Setup(x => x.Contains(topicName)).Returns(true);

        // Act
        await InvokeEnsureTopic(_subject, topicName, false, [], [], true, CancellationToken.None);

        // Assert
        _snsClientMock.Verify(x => x.CreateTopicAsync(It.IsAny<CreateTopicRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(x => x.LookupTopic(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_EnsureTopic_Then_LookupIsCalledAndMetaIsSet_Given_TopicIsNotInCache()
    {
        // Arrange
        var topicName = "test-topic";
        _cacheMock.Setup(x => x.Contains(topicName)).Returns(false);
        var topicMeta = new SqsPathMeta(null, "arn:aws:sns:us-east-1:123456789012:test-topic", PathKind.Topic);
        _cacheMock.Setup(x => x.LookupTopic(topicName, It.IsAny<CancellationToken>())).ReturnsAsync(topicMeta);

        // Act
        await InvokeEnsureTopic(_subject, topicName, false, [], [], true, CancellationToken.None);

        // Assert
        _cacheMock.Verify(x => x.LookupTopic(topicName, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.SetMeta(topicName, topicMeta), Times.Once);
        _snsClientMock.Verify(x => x.CreateTopicAsync(It.IsAny<CreateTopicRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_EnsureTopic_Then_TopicIsCreated_Given_TopicDoesNotExistAndCanCreate()
    {
        // Arrange
        var topicName = "test-topic";
        _cacheMock.Setup(x => x.Contains(topicName)).Returns(false);
        _cacheMock.Setup(x => x.LookupTopic(topicName, It.IsAny<CancellationToken>())).ReturnsAsync((SqsPathMeta)null);

        var createTopicResponse = new CreateTopicResponse { TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic" };
        _snsClientMock.Setup(x => x.CreateTopicAsync(It.IsAny<CreateTopicRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createTopicResponse);

        // Act
        await InvokeEnsureTopic(_subject, topicName, true, [], [], true, CancellationToken.None);

        // Assert
        _snsClientMock.Verify(x => x.CreateTopicAsync(
            It.Is<CreateTopicRequest>(req => req.Name == topicName && req.Attributes.ContainsKey("FifoTopic")),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.SetMeta(
            topicName,
            It.Is<SqsPathMeta>(m => m.Arn == createTopicResponse.TopicArn && m.PathKind == PathKind.Topic)),
            Times.Once);
    }

    [Fact]
    public async Task When_EnsureSubscription_Then_SubscriptionIsCreated_Given_ItDoesNotExistAndCanCreate()
    {
        // Arrange
        var topicName = "test-topic";
        var queueName = "test-queue";
        var filterPolicy = "{\"attribute\":[\"value\"]}";

        var topicMeta = new SqsPathMeta(null, "arn:aws:sns:us-east-1:123456789012:test-topic", PathKind.Topic);
        var queueMeta = new SqsPathMeta("https://sqs.us-east-1.amazonaws.com/123456789012/test-queue", "arn:aws:sqs:us-east-1:123456789012:test-queue", PathKind.Queue);

        _cacheMock.Setup(x => x.GetMetaWithPreloadOrException(topicName, PathKind.Topic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topicMeta);
        _cacheMock.Setup(x => x.GetMetaWithPreloadOrException(queueName, PathKind.Queue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueMeta);

        var listSubscriptionsResponse = new ListSubscriptionsByTopicResponse { Subscriptions = [] };
        _snsClientMock.Setup(x => x.ListSubscriptionsByTopicAsync(topicMeta.Arn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listSubscriptionsResponse);

        // Act
        await InvokeEnsureSubscription(_subject, topicName, queueName, filterPolicy, true, CancellationToken.None);

        // Assert
        _sqsClientMock.Verify(x => x.SetQueueAttributesAsync(
            queueMeta.Url,
            It.Is<Dictionary<string, string>>(attrs => attrs.ContainsKey(QueueAttributeName.Policy)),
            It.IsAny<CancellationToken>()), Times.Once);

        _snsClientMock.Verify(x => x.SubscribeAsync(
            It.Is<SubscribeRequest>(req =>
                req.TopicArn == topicMeta.Arn &&
                req.Protocol == "sqs" &&
                req.Endpoint == queueMeta.Arn &&
                req.Attributes.ContainsKey("FilterPolicy") &&
                req.Attributes["FilterPolicy"] == filterPolicy),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_EnsureSubscription_Then_LogsWarning_Given_CannotCreate()
    {
        // Arrange
        var topicName = "test-topic";
        var queueName = "test-queue";

        var topicMeta = new SqsPathMeta(null, "arn:aws:sns:us-east-1:123456789012:test-topic", PathKind.Topic);
        var queueMeta = new SqsPathMeta("https://sqs.us-east-1.amazonaws.com/123456789012/test-queue", "arn:aws:sqs:us-east-1:123456789012:test-queue", PathKind.Queue);

        _cacheMock.Setup(x => x.GetMetaWithPreloadOrException(topicName, PathKind.Topic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topicMeta);
        _cacheMock.Setup(x => x.GetMetaWithPreloadOrException(queueName, PathKind.Queue, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueMeta);

        var listSubscriptionsResponse = new ListSubscriptionsByTopicResponse { Subscriptions = [] };
        _snsClientMock.Setup(x => x.ListSubscriptionsByTopicAsync(topicMeta.Arn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listSubscriptionsResponse);

        // Act
        await InvokeEnsureSubscription(_subject, topicName, queueName, null, false, CancellationToken.None);

        // Assert
        _sqsClientMock.Verify(x => x.SetQueueAttributesAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _snsClientMock.Verify(x => x.SubscribeAsync(
            It.IsAny<SubscribeRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);

        //_loggerMock.Verify(logger => logger.Log(LogLevel.Warning, "Cannot create subscription for topic {TopicName} and queue {QueueName} as the provider does not allow it", topicName, queueName));
    }

    [Fact]
    public async Task When_DoProvisionTopology_Then_CreatesAllEntities_Given_ProducersAndConsumers()
    {
        // Arrange

        _messageBusSettings.Producers.Add(new()
        {
            MessageType = typeof(TestMessage),
            DefaultPath = "test-queue",
            PathKind = PathKind.Queue,
            Properties =
                {
                    [SqsProperties.EnableFifo.Key] = true,
                    [SqsProperties.VisibilityTimeout.Key] = 30
                }
        });
        _messageBusSettings.Producers.Add(new()
        {
            MessageType = typeof(TestMessage2),
            DefaultPath = "test-topic",
            PathKind = PathKind.Topic,
            Properties =
            {
                [SqsProperties.EnableFifo.Key] = true
            }
        });

        _messageBusSettings.Consumers.Add(new()
        {
            MessageType = typeof(TestMessage),
            Properties =
                {
                    [SqsProperties.UnderlyingQueue.Key] = "consumer-queue",
                    [SqsProperties.SubscribeToTopic.Key] = "test-topic",
                    [SqsProperties.SubscribeToTopicFilterPolicy.Key] = "{\"attr\":\"value\"}",
                    [SqsProperties.EnableFifo.Key] = true
                }
        });

        SqsProperties.Attributes.Set(_messageBusSettings, []);

        _cacheMock.Setup(x => x.Contains(It.IsAny<string>())).Returns(false);
        _cacheMock.Setup(x => x.LookupQueue(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((SqsPathMeta)null);
        _cacheMock.Setup(x => x.LookupTopic(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((SqsPathMeta)null);

        var createQueueResponse = new CreateQueueResponse { QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/test-queue" };
        _sqsClientMock.Setup(x => x.CreateQueueAsync(It.IsAny<CreateQueueRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(createQueueResponse);

        var getQueueAttributesResponse = new GetQueueAttributesResponse
        {
            Attributes = { { QueueAttributeName.QueueArn, "arn:aws:sqs:us-east-1:123456789012:test-queue" } }
        };
        _sqsClientMock.Setup(x => x.GetQueueAttributesAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(getQueueAttributesResponse);

        var createTopicResponse = new CreateTopicResponse { TopicArn = "arn:aws:sns:us-east-1:123456789012:test-topic" };
        _snsClientMock.Setup(x => x.CreateTopicAsync(It.IsAny<CreateTopicRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(createTopicResponse);

        var topicMeta = new SqsPathMeta(null, "arn:aws:sns:us-east-1:123456789012:test-topic", PathKind.Topic);
        var queueMeta = new SqsPathMeta("https://sqs.us-east-1.amazonaws.com/123456789012/consumer-queue", "arn:aws:sqs:us-east-1:123456789012:consumer-queue", PathKind.Queue);

        _cacheMock.Setup(x => x.GetMetaWithPreloadOrException("test-topic", PathKind.Topic, It.IsAny<CancellationToken>()))
                .ReturnsAsync(topicMeta);
        _cacheMock.Setup(x => x.GetMetaWithPreloadOrException("consumer-queue", PathKind.Queue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(queueMeta);

        var listSubscriptionsResponse = new ListSubscriptionsByTopicResponse { Subscriptions = [] };
        _snsClientMock.Setup(x => x.ListSubscriptionsByTopicAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(listSubscriptionsResponse);

        // Act
        await InvokeDoProvisionTopology(_subject, CancellationToken.None);

        // Assert
        _sqsClientMock.Verify(x => x.CreateQueueAsync(It.Is<CreateQueueRequest>(req => req.QueueName == "test-queue"), It.IsAny<CancellationToken>()), Times.Once);
        _snsClientMock.Verify(x => x.CreateTopicAsync(It.Is<CreateTopicRequest>(req => req.Name == "test-topic"), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _sqsClientMock.Verify(x => x.CreateQueueAsync(It.Is<CreateQueueRequest>(req => req.QueueName == "consumer-queue"), It.IsAny<CancellationToken>()), Times.Once);
        _snsClientMock.Verify(x => x.SubscribeAsync(It.Is<SubscribeRequest>(req => req.TopicArn == topicMeta.Arn && req.Endpoint == queueMeta.Arn), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Helper methods to invoke private methods via reflection

    private static string InvokeGetQueuePolicyForTopic(SqsTopologyService service, string queueArn, string topicArn)
    {
        var methodInfo = typeof(SqsTopologyService).GetMethod("GetQueuePolicyForTopic",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        return (string)methodInfo.Invoke(null, [queueArn, topicArn]);
    }

    private static Task InvokeEnsureQueue(SqsTopologyService service, string queue, bool fifo, int? visibilityTimeout,
        string policy, Dictionary<string, string> attributes, Dictionary<string, string> tags, bool canCreate,
        CancellationToken cancellationToken)
    {
        var methodInfo = typeof(SqsTopologyService).GetMethod("EnsureQueue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return (Task)methodInfo.Invoke(service,
        [
            queue, fifo, visibilityTimeout, policy, attributes, tags, canCreate, cancellationToken
        ]);
    }

    private static Task InvokeEnsureTopic(SqsTopologyService service, string topic, bool fifo,
        Dictionary<string, string> attributes, Dictionary<string, string> tags, bool canCreate,
        CancellationToken cancellationToken)
    {
        var methodInfo = typeof(SqsTopologyService).GetMethod("EnsureTopic",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return (Task)methodInfo.Invoke(service,
        [
            topic, fifo, attributes, tags, canCreate, cancellationToken
        ]);
    }

    private static Task InvokeEnsureSubscription(SqsTopologyService service, string topic, string queue,
        string filterPolicy, bool canCreate, CancellationToken cancellationToken)
    {
        var methodInfo = typeof(SqsTopologyService).GetMethod("EnsureSubscription",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return (Task)methodInfo.Invoke(service,
        [
            topic, queue, filterPolicy, canCreate, cancellationToken
        ]);
    }

    private static Task InvokeDoProvisionTopology(SqsTopologyService service, CancellationToken cancellationToken)
    {
        var methodInfo = typeof(SqsTopologyService).GetMethod("DoProvisionTopology",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return (Task)methodInfo.Invoke(service, [cancellationToken]);
    }

    // Test message classes
    private class TestMessage { }
    private class TestMessage2 { }
}
