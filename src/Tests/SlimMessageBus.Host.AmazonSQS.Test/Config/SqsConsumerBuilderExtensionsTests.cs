namespace SlimMessageBus.Host.AmazonSQS.Test.Config;

public class SqsConsumerBuilderExtensionsTests
{
    private readonly ConsumerBuilder<string> _consumerBuilder;
    private readonly ConsumerSettings _consumerSettings;

    public SqsConsumerBuilderExtensionsTests()
    {
        _consumerBuilder = new(new MessageBusSettings());
        _consumerSettings = _consumerBuilder.ConsumerSettings;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_QueueAndSubscribeToTopicConfigured_Given_AnyOrder_Then_PathKindShouldBeTopicAndQueueNameCaptured(bool subscribeToTopicFirst)
    {
        // Arrange
        var queue = "test-queue";
        var topic = "test-topic";

        // Act
        if (subscribeToTopicFirst)
        {
            _consumerBuilder.SubscribeToTopic(topic);
            _consumerBuilder.Queue(queue);
        }
        else
        {
            _consumerBuilder.Queue(queue);
            _consumerBuilder.SubscribeToTopic(topic);
        }

        // Assert
        _consumerSettings.PathKind.Should().Be(PathKind.Topic);
        _consumerSettings.Path.Should().Be(topic);
        _consumerSettings.GetOrDefault(SqsProperties.UnderlyingQueue).Should().Be(queue);
    }

    [Fact]
    public void When_QueueOnlyConfigured_Then_PathKindShouldBeQueueAndQueueNameCaptured()
    {
        // Arrange
        var queue = "test-queue";

        // Act
        _consumerBuilder.Queue(queue);

        // Assert
        _consumerSettings.PathKind.Should().Be(PathKind.Queue);
        _consumerSettings.Path.Should().Be(queue);
        _consumerSettings.GetOrDefault(SqsProperties.UnderlyingQueue).Should().Be(queue);
    }
}
