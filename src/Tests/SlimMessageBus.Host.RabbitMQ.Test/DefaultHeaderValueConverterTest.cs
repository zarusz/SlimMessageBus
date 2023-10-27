namespace SlimMessageBus.Host.RabbitMQ.Test;

public class DefaultHeaderValueConverterTest
{
    [Fact]
    public void When_ConvertTo_Given_StringType_Then_PreservesTheValueButConvertsToByteArray()
    {
        // arrange
        var value = "string";
        var subject = new DefaultHeaderValueConverter();

        // act
        var toValue = subject.ConvertTo(value);
        var fromValue = subject.ConvertFrom(toValue);

        // assert
        toValue.Should().BeOfType<byte[]>();
        fromValue.Should().Be(value);
    }

    [Theory]
    [InlineData(10L)]
    [InlineData(true)]
    [InlineData(10.1f)]
    public void When_ConvertTo_Given_NonStringType_Then_PreservesTheType(object value)
    {
        // arrange
        var subject = new DefaultHeaderValueConverter();

        // act
        var toValue = subject.ConvertTo(value);
        var fromValue = subject.ConvertFrom(toValue);

        // assert
        toValue.Should().Be(value);
        fromValue.Should().Be(value);
    }

}