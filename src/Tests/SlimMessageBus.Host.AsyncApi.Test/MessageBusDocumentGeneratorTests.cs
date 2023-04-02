namespace SlimMessageBus.Host.AsyncApi.Test;

using SlimMessageBus.Host.Test.Messages;

public class MessageBusDocumentGeneratorTests
{
    [Fact]
    public void When_GetMessageId_Given_GenericType_Then_ReturnsTypeName()
    {
        // arrange
        var messageType = typeof(Envelope<SomeMessage2>);

        // act
        var messageId = MessageBusDocumentGenerator.GetMessageId(messageType);

        // assert
        messageId.Should().Be("Envelope`1[[SomeMessage2]]");
    }

    [Fact]
    public void When_GetMessageId_Given_NonGenericType_Then_ReturnsTypeName()
    {
        // arrange
        var messageType = typeof(SomeMessage2);

        // act
        var messageId = MessageBusDocumentGenerator.GetMessageId(messageType);

        // assert
        messageId.Should().Be("SomeMessage2");
    }
}