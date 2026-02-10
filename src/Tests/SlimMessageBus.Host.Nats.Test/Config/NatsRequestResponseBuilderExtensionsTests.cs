namespace SlimMessageBus.Host.Nats.Test.Config;

using SlimMessageBus.Host.Nats;

public class NatsRequestResponseBuilderExtensionsTests
{
    private readonly RequestResponseSettings _settings;
    private readonly RequestResponseBuilder _requestResponseBuilder;

    public NatsRequestResponseBuilderExtensionsTests()
    {
        _settings = new RequestResponseSettings();
        _requestResponseBuilder = new RequestResponseBuilder(_settings);
    }

    #region ReplyToQueue Tests

    [Fact]
    public void When_ReplyToQueue_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => NatsRequestResponseBuilderExtensions.ReplyToQueue(null, "test-queue");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsBuilder");
    }

    [Fact]
    public void When_ReplyToQueue_Given_NullQueueName_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _requestResponseBuilder.ReplyToQueue(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsQueue");
    }

    [Fact]
    public void When_ReplyToQueue_Given_ValidParameters_Then_SetsDefaultPathAndQueuePathKind()
    {
        // Arrange
        var queueName = "test-queue";

        // Act
        var result = _requestResponseBuilder.ReplyToQueue(queueName);

        // Assert
        result.Should().BeSameAs(_requestResponseBuilder);
        _requestResponseBuilder.Settings.Path.Should().Be(queueName);
        _requestResponseBuilder.Settings.PathKind.Should().Be(PathKind.Queue);
    }

    #endregion

    #region ReplyToQueue with builderConfig Tests

    [Fact]
    public void When_ReplyToQueue_BuilderConfig_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => NatsRequestResponseBuilderExtensions.ReplyToQueue(null, "test-queue", (_) => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsBuilder");
    }

    [Fact]
    public void When_ReplyToQueue_BuilderConfig_Given_NullQueueName_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _requestResponseBuilder.ReplyToQueue(null, (_) => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsQueue");
    }

    [Fact]
    public void When_ReplyToQueue_BuilderConfig_Given_NullBuilderConfig_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _requestResponseBuilder.ReplyToQueue("test-queue", null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsBuilderConfig");
    }

    [Fact]
    public void When_ReplyToQueue_BuilderConfig_Given_ValidParameters_Then_SetsDefaultPathAndQueuePathKind()
    {
        // Arrange
        var queueName = "test-queue";

        // Act
        var result = _requestResponseBuilder.ReplyToQueue(queueName, (_) => { });

        // Assert
        result.Should().BeSameAs(_requestResponseBuilder);
        _requestResponseBuilder.Settings.Path.Should().Be(queueName);
        _requestResponseBuilder.Settings.PathKind.Should().Be(PathKind.Queue);
    }

    #endregion
}