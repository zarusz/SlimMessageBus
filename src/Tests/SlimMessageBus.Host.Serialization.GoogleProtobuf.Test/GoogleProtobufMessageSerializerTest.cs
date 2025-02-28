namespace SlimMessageBus.Host.Serialization.GoogleProtobuf.Test;

using FluentAssertions;

using global::Test;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

public class GoogleProtobufMessageSerializerTest
{
    [Fact]
    public void Serialize_When_Object_Then_ProtoMessage()
    {
        // arrange
        var serializer = new GoogleProtobufMessageSerializer(new NullLoggerFactory());

        // act
        var personMessage = new PersonMessage
        {
            Id = 1,
            Name = "SlimMessageBus"
        };
        var serializedPerson = serializer.Serialize(personMessage.GetType(), personMessage, It.IsAny<IMessageContext>());

        // assert
        var deserializedPerson = serializer.Deserialize(typeof(PersonMessage), serializedPerson, It.IsAny<IMessageContext>());
        ((PersonMessage)deserializedPerson).Should().BeEquivalentTo(personMessage);
    }
}
