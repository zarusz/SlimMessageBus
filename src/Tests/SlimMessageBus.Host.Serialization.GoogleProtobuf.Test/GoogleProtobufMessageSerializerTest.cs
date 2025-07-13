namespace SlimMessageBus.Host.Serialization.GoogleProtobuf.Test;

using AwesomeAssertions;

using global::Test;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

public class GoogleProtobufMessageSerializerTest
{
    [Fact]
    public void Serialize_When_Object_Then_ProtoMessage()
    {
        // arrange
        var serializer = new GoogleProtobufMessageSerializer(new NullLoggerFactory());
        var headers = new Dictionary<string, object>();

        // act
        var personMessage = new PersonMessage
        {
            Id = 1,
            Name = "SlimMessageBus"
        };
        var serializedPerson = serializer.Serialize(personMessage.GetType(), headers, personMessage, null);

        // assert
        var deserializedPerson = serializer.Deserialize(typeof(PersonMessage), headers, serializedPerson, null);
        ((PersonMessage)deserializedPerson).Should().BeEquivalentTo(personMessage);
    }
}
