using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Test;
using Xunit;

namespace SlimMessageBus.Host.Serialization.GoogleProtobuf.Test
{
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
            var serializedPerson = serializer.Serialize(personMessage.GetType(), personMessage);

            // assert
            var deserializedPerson = serializer.Deserialize(typeof(PersonMessage), serializedPerson);
            ((PersonMessage) deserializedPerson).Should().BeEquivalentTo(personMessage);
        }
    }
}