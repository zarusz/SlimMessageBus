namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

public class ObjectToInferredTypesConverterTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Converters = { new ObjectToInferredTypesConverter() }
    };

    [Fact]
    public void When_Read_Given_True_Then_ReturnsTrue()
    {
        var result = JsonSerializer.Deserialize<object>("true", _options);
        result.Should().Be(true);
    }

    [Fact]
    public void When_Read_Given_False_Then_ReturnsFalse()
    {
        var result = JsonSerializer.Deserialize<object>("false", _options);
        result.Should().Be(false);
    }

    [Fact]
    public void When_Read_Given_Integer_Then_ReturnsLong()
    {
        var result = JsonSerializer.Deserialize<object>("42", _options);
        result.Should().Be(42L);
    }

    [Fact]
    public void When_Read_Given_DecimalNumber_Then_ReturnsDouble()
    {
        var result = JsonSerializer.Deserialize<object>("3.14", _options);
        result.Should().BeOfType<double>().Which.Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void When_Read_Given_DateTimeString_Then_ReturnsDateTime()
    {
        var result = JsonSerializer.Deserialize<object>("\"2024-01-15T10:30:00\"", _options);
        result.Should().BeOfType<DateTime>().Which.Should().Be(new DateTime(2024, 1, 15, 10, 30, 0));
    }

    [Fact]
    public void When_Read_Given_RegularString_Then_ReturnsString()
    {
        var result = JsonSerializer.Deserialize<object>("\"hello world\"", _options);
        result.Should().Be("hello world");
    }

    [Fact]
    public void When_Read_Given_JsonObject_Then_ReturnsJsonElement()
    {
        var result = JsonSerializer.Deserialize<object>("{\"key\":\"value\"}", _options);
        result.Should().BeOfType<JsonElement>();
    }

    [Fact]
    public void When_Write_Given_HeterogeneousDictionary_Then_RoundTripsCorrectly()
    {
        // arrange
        var dict = new Dictionary<string, object>
        {
            ["str"] = "hello",
            ["num"] = 99L,
            ["flag"] = true
        };

        // act
        var json = JsonSerializer.Serialize<object>(dict, _options);

        // assert: round-trip the values back
        var roundTripped = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _options);
        roundTripped!["str"].Should().Be("hello");
        roundTripped["num"].Should().Be(99L);
        roundTripped["flag"].Should().Be(true);
    }
}
