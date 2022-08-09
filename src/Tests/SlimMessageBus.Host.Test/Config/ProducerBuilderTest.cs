namespace SlimMessageBus.Host.Test.Config;

using SlimMessageBus.Host.Config;

public class ProducerBuilderTest
{
    [Fact]
    public void BuildsProperSettings()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(16);
        var topic = "default-topic";
        var publisherSettings = new ProducerSettings();

        // act
        new ProducerBuilder<SomeMessage>(publisherSettings)
            .DefaultTimeout(timeout)
            .DefaultTopic(topic);

        publisherSettings.MessageType.Should().Be(typeof(SomeMessage));
        publisherSettings.Timeout.Should().Be(timeout);
        publisherSettings.DefaultPath.Should().Be(topic);
    }
}
