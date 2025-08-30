namespace SlimMessageBus.Host.RabbitMQ.Test;
using System;

using AwesomeAssertions;

using Xunit;

public class RabbitMqMessageBusSettingsValidationServiceTests
{
    internal class TestableRabbitMqValidationService : RabbitMqMessageBusSettingsValidationService
    {
        public TestableRabbitMqValidationService(MessageBusSettings settings, RabbitMqMessageBusSettings providerSettings)
            : base(settings, providerSettings)
        {
        }

        // Public wrapper to call the protected method
        public new void AssertConsumer(ConsumerSettings consumerSettings)
        {
            base.AssertConsumer(consumerSettings);
        }
    }

    private TestableRabbitMqValidationService CreateService()
    {
        var busSettings = new MessageBusSettings();
        var providerSettings = new RabbitMqMessageBusSettings();
        return new TestableRabbitMqValidationService(busSettings, providerSettings);
    }

    [Fact]
    public void AssertConsumer_ShouldThrowArgumentNullException_WhenConsumerSettingsIsNull()
    {
        var service = CreateService();

        service.Invoking(s => s.AssertConsumer(null!))
               .Should().Throw<ArgumentNullException>()
               .WithMessage("*consumerSettings*");
    }

    [Fact]
    public void AssertConsumer_ShouldThrow_WhenMessageTypeIsNull()
    {
        var service = CreateService();
        var consumerSettings = new ConsumerSettings
        {
            ConsumerType = typeof(object),
            Path = "exchange-name",
        };

        var expectedMessage = $"Consumer (): The MessageType is not set";

        service.Invoking(s => s.AssertConsumer(consumerSettings))
               .Should().Throw<ConfigurationMessageBusException>()
               .WithMessage(expectedMessage);
    }

    [Fact]
    public void AssertConsumer_ShouldThrow_WhenConsumerTypeIsNull()
    {
        var service = CreateService();
        var consumerSettings = new ConsumerSettings
        {
            MessageType = typeof(object),
            Path = "exchange-name",
        };

        var expectedMessage = $"Consumer (Object): The ConsumerType is not set";

        service.Invoking(s => s.AssertConsumer(consumerSettings))
               .Should().Throw<ConfigurationMessageBusException>()
               .WithMessage(expectedMessage);
    }

    [Fact]
    public void AssertConsumer_ShouldThrow_WhenConsumerMethodIsNull()
    {
        var service = CreateService();
        var consumerSettings = new ConsumerSettings
        {
            MessageType = typeof(object),
            ConsumerType = typeof(object),
            Path = "exchange-name",
        };

        var expectedMessage = $"Consumer (Object): The ConsumerMethod is not set";

        service.Invoking(s => s.AssertConsumer(consumerSettings))
               .Should().Throw<ConfigurationMessageBusException>()
               .WithMessage(expectedMessage);
    }

    [Fact]
    public void AssertConsumer_ShouldThrow_WhenPathIsNull()
    {
        var service = CreateService();
        var consumerSettings = new ConsumerSettings
        {
            MessageType = typeof(object),
            ConsumerType = typeof(object),
            ConsumerMethod = new ConsumerMethod((q, w, e, r) => { return Task.CompletedTask; }),
            Path = null,
        };

        var expectedMessage = $"Consumer (Object): The ExchangeBinding is not set";

        service.Invoking(s => s.AssertConsumer(consumerSettings))
               .Should().Throw<ConfigurationMessageBusException>()
               .WithMessage(expectedMessage);
    }

    [Fact]
    public void AssertConsumer_ShouldThrow_WhenQueueNameIsNull()
    {
        var service = CreateService();
        var consumerSettings = new ConsumerSettings
        {
            MessageType = typeof(object),
            ConsumerType = typeof(object),
            ConsumerMethod = new ConsumerMethod((q, w, e, r) => { return Task.CompletedTask; }),
            Path = "exchange-name"
        };

        var expectedMessage = $"Consumer (Object): The Queue is not set";

        service.Invoking(s => s.AssertConsumer(consumerSettings))
                       .Should().Throw<ConfigurationMessageBusException>()
                       .WithMessage(expectedMessage);
    }
}
