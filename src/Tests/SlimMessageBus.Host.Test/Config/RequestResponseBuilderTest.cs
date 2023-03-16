namespace SlimMessageBus.Host.Test.Config;
public class RequestResponseBuilderTest
{
    [Fact]
    public void BuildsProperSettings()
    {
        // arrange
        var topic = "topic";
        var timeout = TimeSpan.FromSeconds(16);
        var settings = new RequestResponseSettings();

        // act
        var subject = new RequestResponseBuilder(settings);
        subject.DefaultTimeout(timeout);
        subject.ReplyToTopic(topic);

        // assert
        settings.Timeout.Should().Be(timeout);
        settings.Path.Should().Be(topic);
    }
}
