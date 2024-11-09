namespace SlimMessageBus.Host.AmazonSQS.Test;

public class DefaultSqsHeaderSerializerTest
{
    public static readonly TheoryData<object, object> Data = new()
    {
        { null, null },
        { 10, 10 },
        { false, false },
        { true, true },
        { "string", "string" },
        { DateTime.Now.Date, DateTime.Now.Date },
        { Guid.Parse("{529194F3-AEAA-497D-A495-C84DD67C2DDA}"), Guid.Parse("{529194F3-AEAA-497D-A495-C84DD67C2DDA}") },
        { Guid.Empty, Guid.Empty },
    };

    [Theory]
    [MemberData(nameof(Data))]
    public void When_Serialize_Given_VariousValueTypes_Then_RestoresTheValue(object value, object expectedValue)
    {
        // arrange
        var serializer = new DefaultSqsHeaderSerializer();
        var key = "key";

        // act
        var result = serializer.Serialize(key, value);
        var resultValue = serializer.Deserialize(key, result);

        // assert
        resultValue.Should().Be(expectedValue);
    }
}
