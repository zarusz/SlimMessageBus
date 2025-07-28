namespace SlimMessageBus.Host.AzureServiceBus.Test;

/// <summary>
/// Tests for <see cref="ServiceBusMessageBusSettingsValidationService"/>.
/// </summary>
public class ServiceBusMessageBusSettingsValidationServiceTests
{
    private readonly MessageBusSettings _settings;
    private readonly ServiceBusMessageBusSettings _providerSettings;
    private readonly ServiceBusMessageBusSettingsValidationService _subject;

    public ServiceBusMessageBusSettingsValidationServiceTests()
    {
        _settings = new MessageBusSettings
        {
            Name = "TestBus",
            ServiceProvider = Mock.Of<IServiceProvider>(),
        };
        _providerSettings = new ServiceBusMessageBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test"
        };
        _subject = new ServiceBusMessageBusSettingsValidationService(_settings, _providerSettings);
    }

    public record SomeMessage;

    #region AssertSettings Tests

    [Fact]
    public void When_AssertSettings_Given_ConnectionStringIsEmpty_Then_ThrowsException()
    {
        // Arrange
        _providerSettings.ConnectionString = string.Empty;

        // Act
        var act = _subject.AssertSettings;

        // Assert
        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*ConnectionString*");
    }

    [Fact]
    public void When_AssertSettings_Given_ConnectionStringIsSet_Then_DoesNotThrow()
    {
        // Act
        var act = _subject.AssertSettings;

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region AssertProducer Tests

    [Fact]
    public void When_AssertProducer_Given_NeitherToTopicNorToQueue_Then_DoesNotThrowException()
    {
        // Arrange
        var producerBuilder = new ProducerBuilder<SomeMessage>(new ProducerSettings());

        // Act
        var act = () => _subject.AssertProducer(producerBuilder.Settings);

        // Assert
        act.Should().NotThrow<ConfigurationMessageBusException>();
    }

    [Fact]
    public void When_AssertProducer_Given_PathKindSetIsNotNull_Then_DoesNotThrow()
    {
        // Arrange
        var producerBuilder = new ProducerBuilder<SomeMessage>(new ProducerSettings())
            .ToQueue();

        // Act
        var act = () => _subject.AssertProducer(producerBuilder.Settings);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region AssertConsumer Tests

    [Fact]
    public void When_AssertConsumer_Given_PathIsEmpty_Then_ThrowsException()
    {
        // Arrange
        var consumerBuilder = new ConsumerBuilder<SomeMessage>(_settings);

        // Act
        var act = () => _subject.AssertConsumer(consumerBuilder.ConsumerSettings);

        // Assert
        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*The Queue or Topic is not set");
    }

    [Fact]
    public void When_AssertConsumer_Given_PathKindIsTopicAndSubscriptionNameIsEmpty_Then_ThrowsException()
    {
        // Arrange
        var consumerBuilder = new ConsumerBuilder<SomeMessage>(_settings)
            .Topic("my-topic")
            .WithConsumer<IConsumer<SomeMessage>>();

        // no subscription name set

        // Act
        var act = () => _subject.AssertConsumer(consumerBuilder.ConsumerSettings);

        // Assert
        act.Should().Throw<ConfigurationMessageBusException>()
            .WithMessage("*SubscriptionName*");
    }

    [Fact]
    public void When_AssertConsumer_Given_PathKindIsQueueAndSubscriptionNameIsEmpty_Then_DoesNotThrow()
    {
        // Arrange
        var consumerBuilder = new ConsumerBuilder<SomeMessage>(_settings)
            .Queue("my-queue")
            .WithConsumer<IConsumer<SomeMessage>>();

        // Act
        var act = () => _subject.AssertConsumer(consumerBuilder.ConsumerSettings);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void When_AssertConsumer_Given_PathKindIsTopicAndSubscriptionNameIsSet_Then_DoesNotThrow()
    {
        // Arrange
        var consumerBuilder = new ConsumerBuilder<SomeMessage>(_settings)
            .Topic("my-topic")
            .WithConsumer<IConsumer<SomeMessage>>()
            .SubscriptionName("my-subscription");

        // Act
        var act = () => _subject.AssertConsumer(consumerBuilder.ConsumerSettings);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void When_AssertConsumer_Given_PathKindIsTopicAndSubscriptionNameIsSetOnProviderLevel_Then_DoesNotThrow()
    {
        // Arrange
        var consumerBuilder = new ConsumerBuilder<SomeMessage>(_settings)
            .Topic("my-topic")
            .WithConsumer<IConsumer<SomeMessage>>();

        // Set subscription name at provider level
        _providerSettings.SubscriptionName("default-subscription");

        // Act
        var act = () => _subject.AssertConsumer(consumerBuilder.ConsumerSettings);

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}