namespace SlimMessageBus.Host.Kafka.Test;

public class DefaultKafkaHeaderSerializerTest
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("abc", "abc", false)]
    [InlineData(123, "123", false)]
    [InlineData(123, 123L, true)]
    [InlineData(123.5, "123.5", false)]
    [InlineData(123.5, 123.5, true)]
    [InlineData(true, "True", false)]
    [InlineData(true, true, true)]
    public void Test(object inValue, object outValue, bool inferType)
    {
        // arrange
        var subject = new DefaultKafkaHeaderSerializer(inferType: inferType);

        // act
        var payload = subject.Serialize(typeof(object), inValue);
        var actualValue = subject.Deserialize(typeof(object), payload);

        // assert
        if (inValue is null)
        {
            actualValue.Should().BeNull();
        }
        else
        {
            actualValue.Should().NotBeNull();
            if (!inferType)
            {
                actualValue.Should().BeOfType<string>();
            }
            actualValue.Should().Be(outValue);
        }
    }
}
