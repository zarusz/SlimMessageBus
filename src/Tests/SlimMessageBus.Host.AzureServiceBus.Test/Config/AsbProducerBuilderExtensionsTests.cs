namespace SlimMessageBus.Host.AzureServiceBus.Test.Config;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public class AsbProducerBuilderExtensionsTests
{
    private class TestMessage { }

    private readonly MessageBusSettings _settings;
    private readonly ProducerBuilder<TestMessage> _producerBuilder;

    public AsbProducerBuilderExtensionsTests()
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
        var action = () => AsbProducerBuilderExtensions.DefaultQueue<TestMessage>(null, "test-queue");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("producerBuilder");
    }

    [Fact]
    public void When_DefaultQueue_Given_NullQueueName_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _producerBuilder.DefaultQueue(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("queue");
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
        var action = () => AsbProducerBuilderExtensions.ToTopic<TestMessage>(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("producerBuilder");
    }

    [Fact]
    public void When_ToTopic_Given_ValidBuilder_Then_SetsTopicPathKind()
    {
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
        var action = () => AsbProducerBuilderExtensions.ToQueue<TestMessage>(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("producerBuilder");
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

    #region WithModifier Tests

    [Fact]
    public void When_WithModifier_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbProducerBuilderExtensions.WithModifier<TestMessage>(null, (_, _) => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("producerBuilder");
    }

    [Fact]
    public void When_WithModifier_Given_NullModifier_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _producerBuilder.WithModifier(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("modifier");
    }

    [Fact]
    public void When_WithModifier_Given_ValidModifier_Then_SetsModifier()
    {
        // Arrange
        var subject = "Test";
        AsbMessageModifier<TestMessage> modifier = (msg, serviceBusMessage) =>
        {
            serviceBusMessage.Subject = subject;
        };

        // Act
        var result = _producerBuilder.WithModifier(modifier);

        // Assert
        result.Should().BeSameAs(_producerBuilder);
        var objectModifier = _producerBuilder.Settings.GetOrDefault(AsbProperties.MessageModifier);
        objectModifier.Should().NotBeNull();

        var message = new TestMessage();
        var transportMessage = new ServiceBusMessage();
        objectModifier(message, transportMessage);

        transportMessage.Subject.Should().Be(subject);
    }

    #endregion

    #region CreateQueueOptions Tests

    [Fact]
    public void When_CreateQueueOptions_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbProducerBuilderExtensions.CreateQueueOptions<TestMessage>(null, _ => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("producerBuilder");
    }

    [Fact]
    public void When_CreateQueueOptions_Given_NullAction_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _producerBuilder.CreateQueueOptions(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("action");
    }

    [Fact]
    public void When_CreateQueueOptions_Given_ValidAction_Then_SetsOptions()
    {
        // Arrange
        Action<CreateQueueOptions> configAction = options => options.AutoDeleteOnIdle = TimeSpan.FromHours(1);

        // Act
        var result = _producerBuilder.CreateQueueOptions(configAction);

        // Assert
        result.Should().BeSameAs(_producerBuilder);
        _producerBuilder.Settings.GetOrDefault(AsbProperties.CreateQueueOptions).Should().BeSameAs(configAction);
    }

    #endregion

    #region CreateTopicOptions Tests

    [Fact]
    public void When_CreateTopicOptions_Given_NullBuilder_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => AsbProducerBuilderExtensions.CreateTopicOptions<TestMessage>(null, _ => { });

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("producerBuilder");
    }

    [Fact]
    public void When_CreateTopicOptions_Given_NullAction_Then_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _producerBuilder.CreateTopicOptions(null);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("action");
    }

    [Fact]
    public void When_CreateTopicOptions_Given_ValidAction_Then_SetsOptions()
    {
        // Arrange
        Action<CreateTopicOptions> configAction = options => options.EnablePartitioning = true;

        // Act
        var result = _producerBuilder.CreateTopicOptions(configAction);

        // Assert
        result.Should().BeSameAs(_producerBuilder);
        _producerBuilder.Settings.GetOrDefault(AsbProperties.CreateTopicOptions).Should().BeSameAs(configAction);
    }

    #endregion
}