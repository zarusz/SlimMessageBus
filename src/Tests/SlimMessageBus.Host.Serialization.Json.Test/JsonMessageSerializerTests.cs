namespace SlimMessageBus.Host.Serialization.Json.Test;

using FluentAssertions;

using Moq;

public class JsonMessageSerializerTests
{
    public static TheoryData<object, object> Data => new()
    {
        { null, null },
        { 10, 10 },
        { false, false },
        { true, true},
        { "string", "string"},
        { DateTime.Now.Date, DateTime.Now.Date},
        { Guid.Empty, "00000000-0000-0000-0000-000000000000"},
    };

    [Theory]
    [MemberData(nameof(Data))]
    public void When_SerializeAndDeserialize_Given_TypeObjectAndBytesPayload_Then_TriesToInferPrimitiveTypes(object value, object expectedValue)
    {
        // arrange
        var subject = new JsonMessageSerializer();

        // act
        var bytes = subject.Serialize(typeof(object), value, It.IsAny<IMessageContext>());
        var deserializedValue = subject.Deserialize(typeof(object), bytes, It.IsAny<IMessageContext>());

        // assert
        deserializedValue.Should().Be(expectedValue);
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void When_SerializeAndDeserialize_Given_TypeObjectAndStringPayload_Then_TriesToInferPrimitiveTypes(object value, object expectedValue)
    {
        // arrange
        var subject = new JsonMessageSerializer() as IMessageSerializer<string>;

        // act
        var json = subject.Serialize(typeof(object), value, It.IsAny<IMessageContext>());
        var deserializedValue = subject.Deserialize(typeof(object), json, It.IsAny<IMessageContext>());

        // assert
        deserializedValue.Should().Be(expectedValue);
    }
}