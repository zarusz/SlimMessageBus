namespace SlimMessageBus.Host.AmazonSQS.Test.ClientFactory;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.SQS.Util;

public class SqsCacheServiceTest
{
    private readonly Mock<ISqsClientProvider> _mockSqsClientProvider;
    private readonly Mock<ISnsClientProvider> _mockSnsClientProvider;
    private readonly Mock<IAmazonSQS> _mockSqsClient;
    private readonly Mock<IAmazonSimpleNotificationService> _mockSnsClient;
    private readonly SqsTopologyCache _sut;

    public SqsCacheServiceTest()
    {
        _mockSqsClient = new Mock<IAmazonSQS>();
        _mockSnsClient = new Mock<IAmazonSimpleNotificationService>();

        _mockSqsClientProvider = new Mock<ISqsClientProvider>();
        _mockSqsClientProvider.Setup(x => x.Client).Returns(_mockSqsClient.Object);

        _mockSnsClientProvider = new Mock<ISnsClientProvider>();
        _mockSnsClientProvider.Setup(x => x.Client).Returns(_mockSnsClient.Object);

        _sut = new SqsTopologyCache(_mockSqsClientProvider.Object, _mockSnsClientProvider.Object);
    }

    #region GetMetaOrException

    [Fact]
    public void When_GetMetaOrException_Then_ReturnsPathMeta_Given_ExistingPath()
    {
        // Arrange
        var path = "test-queue";
        var expectedMeta = new SqsPathMeta("http://queue-url", "arn:test-queue", PathKind.Queue);
        _sut.SetMeta(path, expectedMeta);

        // Act
        var result = _sut.GetMetaOrException(path);

        // Assert
        result.Should().Be(expectedMeta);
    }

    [Fact]
    public void When_GetMetaOrException_Then_ThrowsException_Given_NonExistingPath()
    {
        // Arrange
        var path = "non-existing-queue";

        // Act & Assert
        _sut.Invoking(x => x.GetMetaOrException(path))
            .Should().Throw<ProducerMessageBusException>()
            .WithMessage($"The {path} has unknown URL/ARN at this point. Ensure the queue exists in Amazon SQS/SNS and the queue or topic is declared in SMB.");
    }

    #endregion

    #region GetMetaWithPreloadOrException

    [Fact]
    public async Task When_GetMetaWithPreloadOrException_Then_ReturnsExistingMeta_Given_PathInCache()
    {
        // Arrange
        var path = "test-queue";
        var expectedMeta = new SqsPathMeta("http://queue-url", "arn:test-queue", PathKind.Queue);
        _sut.SetMeta(path, expectedMeta);

        // Act
        var result = await _sut.GetMetaWithPreloadOrException(path, PathKind.Queue, CancellationToken.None);

        // Assert
        result.Should().Be(expectedMeta);

        // Verify no lookups were performed
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockSnsClient.Verify(x => x.FindTopicAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task When_GetMetaWithPreloadOrException_Then_PerformsLookupAndCaches_Given_QueueNotInCache()
    {
        // Arrange
        var path = "test-queue";
        var queueUrl = "http://test-queue-url";
        var queueArn = "arn:test-queue";

        _mockSqsClient.Setup(x => x.GetQueueUrlAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.Setup(x => x.GetQueueAttributesAsync(queueUrl, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQueueAttributesResponse(queueArn));

        // Act
        var result = await _sut.GetMetaWithPreloadOrException(path, PathKind.Queue, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be(queueUrl);
        result.Arn.Should().Be(queueArn);
        result.PathKind.Should().Be(PathKind.Queue);

        // Verify the cache contains the entry
        _sut.Contains(path).Should().BeTrue();

        // Verify lookups were performed
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(path, It.IsAny<CancellationToken>()), Times.Once);
        _mockSqsClient.Verify(x => x.GetQueueAttributesAsync(queueUrl, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_GetMetaWithPreloadOrException_Then_PerformsLookupAndCaches_Given_TopicNotInCache()
    {
        // Arrange
        var path = "test-topic";
        var topicArn = "arn:test-topic";

        _mockSnsClient.Setup(x => x.FindTopicAsync(path))
            .ReturnsAsync(new Topic { TopicArn = topicArn });

        // Act
        var result = await _sut.GetMetaWithPreloadOrException(path, PathKind.Topic, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().BeNull();
        result.Arn.Should().Be(topicArn);
        result.PathKind.Should().Be(PathKind.Topic);

        // Verify the cache contains the entry
        _sut.Contains(path).Should().BeTrue();

        // Verify lookups were performed
        _mockSnsClient.Verify(x => x.FindTopicAsync(path), Times.Once);
    }

    private static GetQueueAttributesResponse CreateQueueAttributesResponse(string queueArn) => new()
    {
        Attributes = { [SQSConstants.ATTRIBUTE_QUEUE_ARN] = queueArn }
    };

    [Fact]
    public async Task When_GetMetaWithPreloadOrException_Then_ThreadSafety_Given_ConcurrentRequests()
    {
        // Arrange
        var path = "test-queue";
        var queueUrl = "http://test-queue-url";
        var queueArn = "arn:test-queue";

        _mockSqsClient.Setup(x => x.GetQueueUrlAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.Setup(x => x.GetQueueAttributesAsync(queueUrl, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQueueAttributesResponse(queueArn));

        // Act - make 5 concurrent calls
        var tasks = new List<Task<SqsPathMeta>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_sut.GetMetaWithPreloadOrException(path, PathKind.Queue, CancellationToken.None));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        // All tasks should return the same result
        foreach (var result in results)
        {
            result.Should().NotBeNull();
            result.Url.Should().Be(queueUrl);
            result.Arn.Should().Be(queueArn);
        }

        // Verify lookups were performed exactly once despite concurrent calls
        _mockSqsClient.Verify(x => x.GetQueueUrlAsync(path, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SetMeta

    [Fact]
    public void When_SetMeta_Then_AddsToCache_Given_NewPath()
    {
        // Arrange
        var path = "test-queue";
        var meta = new SqsPathMeta("http://queue-url", "arn:test-queue", PathKind.Queue);

        // Act
        _sut.SetMeta(path, meta);

        // Assert
        _sut.Contains(path).Should().BeTrue();
        _sut.GetMetaOrException(path).Should().Be(meta);
    }

    [Fact]
    public void When_SetMeta_Then_DoesNotModifyCache_Given_ExistingPathWithSameKind()
    {
        // Arrange
        var path = "test-queue";
        var originalMeta = new SqsPathMeta("http://queue-url-original", "arn:test-queue-original", PathKind.Queue);
        var newMeta = new SqsPathMeta("http://queue-url-new", "arn:test-queue-new", PathKind.Queue);

        // Add original meta
        _sut.SetMeta(path, originalMeta);

        // Act
        _sut.SetMeta(path, newMeta);

        // Assert
        _sut.Contains(path).Should().BeTrue();
        var result = _sut.GetMetaOrException(path);
        result.Should().Be(originalMeta); // Original meta should remain
    }

    [Fact]
    public void When_SetMeta_Then_ThrowsException_Given_ExistingPathWithDifferentKind()
    {
        // Arrange
        var path = "test-path";
        var queueMeta = new SqsPathMeta("http://queue-url", "arn:test-queue", PathKind.Queue);
        var topicMeta = new SqsPathMeta(null, "arn:test-topic", PathKind.Topic);

        // Add queue meta first
        _sut.SetMeta(path, queueMeta);

        // Act & Assert
        _sut.Invoking(x => x.SetMeta(path, topicMeta))
            .Should().Throw<ConfigurationMessageBusException>()
            .WithMessage($"Path {path} is declared as both {PathKind.Queue} and {PathKind.Topic}");
    }

    #endregion

    #region Contains

    [Fact]
    public void When_Contains_Then_ReturnsTrue_Given_ExistingPath()
    {
        // Arrange
        var path = "test-queue";
        var meta = new SqsPathMeta("http://queue-url", "arn:test-queue", PathKind.Queue);
        _sut.SetMeta(path, meta);

        // Act
        var result = _sut.Contains(path);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void When_Contains_Then_ReturnsFalse_Given_NonExistingPath()
    {
        // Arrange
        var path = "non-existing-queue";

        // Act
        var result = _sut.Contains(path);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region LookupQueue

    [Fact]
    public async Task When_LookupQueue_Then_ReturnsMeta_Given_ExistingQueue()
    {
        // Arrange
        var path = "test-queue";
        var queueUrl = "http://test-queue-url";
        var queueArn = "arn:test-queue";

        _mockSqsClient.Setup(x => x.GetQueueUrlAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl });

        _mockSqsClient.Setup(x => x.GetQueueAttributesAsync(queueUrl, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQueueAttributesResponse(queueArn));

        // Act
        var result = await _sut.LookupQueue(path, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be(queueUrl);
        result.Arn.Should().Be(queueArn);
        result.PathKind.Should().Be(PathKind.Queue);
    }

    [Fact]
    public async Task When_LookupQueue_Then_ReturnsNull_Given_NonExistingQueue()
    {
        // Arrange
        var path = "non-existing-queue";

        _mockSqsClient.Setup(x => x.GetQueueUrlAsync(path, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new QueueDoesNotExistException("Queue does not exist"));

        // Act
        var result = await _sut.LookupQueue(path, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region LookupTopic

    [Fact]
    public async Task When_LookupTopic_Then_ReturnsMeta_Given_ExistingTopic()
    {
        // Arrange
        var path = "test-topic";
        var topicArn = "arn:test-topic";

        _mockSnsClient.Setup(x => x.FindTopicAsync(path))
            .ReturnsAsync(new Topic { TopicArn = topicArn });

        // Act
        var result = await _sut.LookupTopic(path, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().BeNull();
        result.Arn.Should().Be(topicArn);
        result.PathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public async Task When_LookupTopic_Then_ReturnsNull_Given_NonExistingTopic()
    {
        // Arrange
        var path = "non-existing-topic";

        _mockSnsClient.Setup(x => x.FindTopicAsync(path))
            .ReturnsAsync((Topic)null);

        // Act
        var result = await _sut.LookupTopic(path, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
