namespace SlimMessageBus.Host.Nats.Test.Config;

using SlimMessageBus.Host.Nats;

public class NatsHandlerBuilderExtensionsTests
{
    public interface ISomeMessageMarkerInterface { }
    public record SomeRequest : IRequest<SomeResponse>, ISomeMessageMarkerInterface;
    public record SomeResponse;

    private readonly MessageBusSettings _messageBusSettings;
    private readonly HandlerBuilder<SomeRequest, SomeResponse> _handlerBuilder;

    public NatsHandlerBuilderExtensionsTests()
    {
        _messageBusSettings = new MessageBusSettings();
        _handlerBuilder = new HandlerBuilder<SomeRequest, SomeResponse>(_messageBusSettings);
    }

    #region Queue Tests

    [Fact]
    public void When_Queue_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => NatsHandlerBuilderExtensions.Queue<SomeRequest, SomeResponse>(null, "test-queue");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsBuilder");
    }

    [Fact]
    public void When_Queue_Given_NullQueueName_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _handlerBuilder.Queue(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("natsQueue");
    }

    [Fact]
    public void When_Queue_Given_ValidParameters_Then_SetsDefaultPathAndQueuePathKind()
    {
        // Arrange
        var queueName = "test-queue";

        // Act
        var result = _handlerBuilder.Queue(queueName);

        // Assert
        result.Should().BeSameAs(_handlerBuilder);
        _handlerBuilder.Settings.Consumers.First().Path.Should().Be(queueName);
        _handlerBuilder.Settings.Consumers.First().PathKind.Should().Be(PathKind.Queue);
    }

    #endregion
}