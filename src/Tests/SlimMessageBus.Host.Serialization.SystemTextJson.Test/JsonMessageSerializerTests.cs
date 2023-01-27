namespace SlimMessageBus.Host.Serialization.SystemTextJson.Test;

using FluentAssertions;

public class JsonMessageSerializerTests
{
    public static IEnumerable<object[]> Data =>
        new List<object[]>
        {
            new object[] { null, null },
            new object[] { 10, 10 },
            new object[] { false, false },
            new object[] { true, true },
            new object[] { "string", "string" },
            new object[] { DateTime.Now.Date, DateTime.Now.Date },
            new object[] { Guid.Empty, "00000000-0000-0000-0000-000000000000" },
        };

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