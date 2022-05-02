using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Test;
using Xunit;

namespace SlimMessageBus.Host.Serialization.Google.Protobuf.Test
{
    public class GoogleProtobufMessageSerializerTest
    {
        [Fact]
        public void Serialize_When_Object_Then_ProtoMessage()
        {

            GoogleProtobufMessageSerializer serializer = new GoogleProtobufMessageSerializer(new NullLoggerFactory());

            PersonMessage personMessage = new PersonMessage()
            {
                Id = 1,
                Name = "SlimMessageBus"
            };

           byte[] serializedPerson =  serializer.Serialize(personMessage.GetType(), personMessage);

           object deserializedPerson = serializer.Deserialize(typeof(PersonMessage), serializedPerson);

           ((PersonMessage) deserializedPerson).Should().BeEquivalentTo(personMessage);
        }
    }
}