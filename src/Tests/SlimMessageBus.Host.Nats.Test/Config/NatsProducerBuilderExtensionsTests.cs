namespace SlimMessageBus.Host.Nats.Test.Config;

using SlimMessageBus.Host.Nats;

public class NatsProducerBuilderExtensionsTests
{
    private class TestMessage { }

    private readonly MessageBusSettings _settings;
    private readonly ProducerBuilder<TestMessage> _producerBuilder;

    public NatsProducerBuilderExtensionsTests()
    {
        _settings = new MessageBusSettings();
        var producerSettings = new ProducerSettings { MessageType = typeof(TestMessage) };
        _settings.Producers.Add(producerSettings);
        _producerBuilder = new ProducerBuilder<TestMessage>(producerSettings);
    }

    #region DefaultQueue Tests

    [Fact]
    public void When_DefaultQueue_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => NatsProducerBuilderExtensions.DefaultQueue<TestMessage>(null, "test-queue");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsProducerBuilder");
    }

    [Fact]
    public void When_DefaultQueue_Given_NullQueueName_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _producerBuilder.DefaultQueue(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsQueue");
    }

    [Fact]
    public void When_DefaultQueue_Given_ValidParameters_Then_SetsDefaultPathAndQueuePathKind()
    {
        // Arrange
        var queueName = "test-queue";

        // Act
        var result = _producerBuilder.DefaultQueue(queueName);

        // Assert
        result.Should().BeSameAs(_producerBuilder);
        _producerBuilder.Settings.DefaultPath.Should().Be(queueName);
        _producerBuilder.Settings.PathKind.Should().Be(PathKind.Queue);
    }

    #endregion

    #region ToTopic Tests

    [Fact]
    public void When_ToTopic_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => NatsProducerBuilderExtensions.ToTopic<TestMessage>(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsProducerBuilder");
    }

    [Fact]
    public void When_ToTopic_Given_ValidBuilder_Then_SetsQueuePathKind()
    {
        // Arrange
        _producerBuilder.Settings.PathKind = PathKind.Queue; // Set to Queue first to test change

        // Act
        var result = _producerBuilder.ToTopic();

        // Assert
        result.Should().BeSameAs(_producerBuilder);
        _producerBuilder.Settings.PathKind.Should().Be(PathKind.Topic);
    }

    #endregion

    #region ToQueue Tests

    [Fact]
    public void When_ToQueue_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => NatsProducerBuilderExtensions.ToQueue<TestMessage>(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsProducerBuilder");
    }

    [Fact]
    public void When_ToQueue_Given_ValidBuilder_Then_SetsQueuePathKind()
    {
        // Arrange
        _producerBuilder.Settings.PathKind = PathKind.Topic; // Set to Topic first to test change

        // Act
        var result = _producerBuilder.ToQueue();

        // Assert
        result.Should().BeSameAs(_producerBuilder);
        _producerBuilder.Settings.PathKind.Should().Be(PathKind.Queue);
    }

    #endregion
}