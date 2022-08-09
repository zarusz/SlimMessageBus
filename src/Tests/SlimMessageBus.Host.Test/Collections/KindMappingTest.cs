namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

public class KindMappingTest
{
    private readonly KindMapping subject = new KindMapping();

    private void Configure(RequestResponseSettings reqRespSettings, IEnumerable<ProducerSettings> producers)
    {
        var settings = new MessageBusSettings
        {
            RequestResponse = reqRespSettings
        };
        foreach (var producer in producers)
        {
            settings.Producers.Add(producer);
        }

        subject.Configure(settings);
    }

    [Fact]
    public void Given_RequestResponseOnTopic_Then_AnyTypeOnThatPathIsReturnedAsTopic()
    {
        // Arrange
        Configure(
            new RequestResponseSettings
            {
                Path = "response-topic",
                PathKind = PathKind.Topic
            },
            new[] {
                new ProducerSettings() { DefaultPath = "topic1", PathKind = PathKind.Topic, MessageType = typeof(SomeMessage) },
                new ProducerSettings() { DefaultPath = "topic2", PathKind = PathKind.Topic, MessageType = typeof(SomeDerivedMessage) },
                new ProducerSettings() { DefaultPath = "queue1", PathKind = PathKind.Queue, MessageType = typeof(SomeDerived2Message) }
            }
        );

        // act
        var pathKind = subject.GetKind(typeof(SomeResponse), "response-topic");

        // assert
        pathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public void Given_RequestResponseOnQueue_Then_AnyTypeOnThatPathIsReturnedAsQueue()
    {
        // Arrange
        Configure(
            new RequestResponseSettings
            {
                Path = "response-queue",
                PathKind = PathKind.Queue
            },
            new[] {
                new ProducerSettings() { DefaultPath = "topic1", PathKind = PathKind.Topic, MessageType = typeof(SomeMessage) },
                new ProducerSettings() { DefaultPath = "topic2", PathKind = PathKind.Topic, MessageType = typeof(SomeDerivedMessage) },
                new ProducerSettings() { DefaultPath = "queue1", PathKind = PathKind.Queue, MessageType = typeof(SomeDerived2Message) }
            }
        );

        // act
        var pathKind = subject.GetKind(typeof(SomeResponse), "response-queue");

        // assert
        pathKind.Should().Be(PathKind.Queue);
    }

    [Fact]
    public void Given_UndeclaredProducerTypeOrPath_Then_TheKindIsTopicByDefault()
    {
        // Arrange
        Configure(
            new RequestResponseSettings
            {
                Path = "response-queue",
                PathKind = PathKind.Queue
            },
            new[] {
                new ProducerSettings() { DefaultPath = "topic1", PathKind = PathKind.Topic, MessageType = typeof(SomeMessage) },
                new ProducerSettings() { DefaultPath = "topic2", PathKind = PathKind.Topic, MessageType = typeof(SomeDerivedMessage) },
            }
        );

        // act
        var pathKind = subject.GetKind(typeof(SomeDerived2Message), "non-existing-topic");

        // assert
        pathKind.Should().Be(PathKind.Topic);
    }

    [Fact]
    public void Given_TwoProducerWithSamePathButOtherKind_Then_ExceptionIsRaised()
    {
        // Arrange

        Action configuration = () => Configure(
            new RequestResponseSettings
            {
                Path = "response-topic",
                PathKind = PathKind.Topic
            },
            new[] {
                new ProducerSettings() { DefaultPath = "message-a", PathKind = PathKind.Topic, MessageType = typeof(SomeMessage) },
                new ProducerSettings() { DefaultPath = "message-a", PathKind = PathKind.Queue, MessageType = typeof(SomeDerivedMessage) },
            }
        );

        // act & assert
        configuration.Should().Throw<ConfigurationMessageBusException>();
    }
}
