namespace SlimMessageBus.Host.Serialization.Json.Test;

using FluentAssertions;

public class JsonMessageSerializerTests
{
    public static IEnumerable<object[]> Data =>
        [
            [null, null],
            [10, 10],
            [false, false],
            [true, true],
            ["string", "string"],
            [DateTime.Now.Date, DateTime.Now.Date],
            [Guid.Empty, "00000000-0000-0000-0000-000000000000"],
        ];

    [Theory]
    [MemberData(nameof(Data))]
    public void When_SerializeAndDeserialize_Given_TypeObject_Then_TriesToInferPrimitiveTypes(object value, object expectedValue)
    {
        // arrange
        var subject = new JsonMessageSerializer();

        // act
        var bytes = subject.Serialize(typeof(object), value);
        var deserializedValue = subject.Deserialize(typeof(object), bytes);

        // assert
        deserializedValue.Should().Be(expectedValue);
    }
}