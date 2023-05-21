namespace SlimMessageBus.Host.Test.Services;

using SlimMessageBus.Host.Services;

public class MessageHeaderServiceTests
{
    private readonly MessageHeaderService _subject;
    private readonly MessageBusSettings _messageBusSettings;
    private readonly MessageBusBuilder _messageBusBuilder;
    private readonly Mock<IMessageTypeResolver> _messageTypeResolverMock;

    public MessageHeaderServiceTests()
    {
        _messageBusBuilder = MessageBusBuilder.Create();
        _messageBusSettings = _messageBusBuilder.Settings;

        _messageTypeResolverMock = new Mock<IMessageTypeResolver>();
        _messageTypeResolverMock.Setup(x => x.ToName(It.IsAny<Type>())).Returns<Type>(type => type.Name);
        _subject = new MessageHeaderService(NullLogger.Instance, _messageBusSettings, _messageTypeResolverMock.Object);
    }

    [Fact]
    public void When_AddMessageTypeHeader_Given_HeadersNotNull_Then_CallsMessageTypeResolver_And_SetsHeader()
    {
        // arrange
        var message = new SomeMessage();
        var headers = new Dictionary<string, object>();

        // act
        _subject.AddMessageTypeHeader(message, headers);

        // assert
        _messageTypeResolverMock.Verify(x => x.ToName(message.GetType()), Times.Once);
        _messageTypeResolverMock.VerifyNoOtherCalls();

        headers.Count.Should().Be(1);
        headers.Should().ContainSingle(x => x.Key == MessageHeaders.MessageType && (string)x.Value == nameof(SomeMessage));
    }

    [Fact]
    public void When_AddMessageHeaders_Given_ExistingHeader_And_ProducerModifier_And_BusModifier_Then_HeadersAreAdded_And_MessageTypeIsAdded_And_BusHeaderModifier_And_ProducerHeaderModifier()
    {
        // arrange
        var message = new SomeMessage();
        var messageHeaders = new Dictionary<string, object>();
        var headers = new Dictionary<string, object>
        {
            ["existing-header"] = true
        };

        _messageBusBuilder
            .WithHeaderModifier<SomeMessage>((h, m) =>
            {
                h["order"] = 1;
                h["bus-header"] = true;
            });

        var producerSettings = new ProducerBuilder<SomeMessage>(new ProducerSettings())
            .DefaultPath("topic")
            .WithHeaderModifier((h, m) =>
            {
                h["order"] = 2;
                h["producer-header"] = true;
            })
            .Settings;

        // act
        _subject.AddMessageHeaders(messageHeaders, headers, message, producerSettings);

        // assert
        messageHeaders.Should().HaveCount(5);
        messageHeaders.Should().Contain(x => x.Key == MessageHeaders.MessageType && (string)x.Value == nameof(SomeMessage));
        messageHeaders.Should().Contain(x => x.Key == "order" && (int)x.Value == 2);
        messageHeaders.Should().Contain(x => x.Key == "bus-header" && (bool)x.Value);
        messageHeaders.Should().Contain(x => x.Key == "producer-header" && (bool)x.Value);
        messageHeaders.Should().Contain(x => x.Key == "existing-header" && (bool)x.Value);
    }
}