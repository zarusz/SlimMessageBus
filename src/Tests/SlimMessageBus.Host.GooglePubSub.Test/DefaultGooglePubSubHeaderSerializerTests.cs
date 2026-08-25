namespace SlimMessageBus.Host.GooglePubSub.Test;

public class DefaultGooglePubSubHeaderSerializerTests
{
    public static readonly TheoryData<object> Values = new()
    {
        null,
        "123",
        true,
        (byte)12,
        (short)123,
        1234,
        12345L,
        12.5f,
        123.456d,
        123.456m,
        Guid.Parse("529194F3-AEAA-497D-A495-C84DD67C2DDA"),
        new DateTime(2026, 8, 25, 12, 30, 0, DateTimeKind.Utc),
        new DateTimeOffset(2026, 8, 25, 12, 30, 0, TimeSpan.FromHours(2))
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void When_HeaderIsSerialized_Then_ValueAndTypeArePreserved(object value)
    {
        var serializer = new DefaultGooglePubSubHeaderSerializer();

        var serialized = serializer.Serialize("header", value);
        var restored = serializer.Deserialize("header", serialized);

        restored.Should().Be(value);
    }

    [Fact]
    public void When_StringLooksLikeAnotherType_Then_ItRemainsAString()
    {
        var serializer = new DefaultGooglePubSubHeaderSerializer();

        serializer.Deserialize("header", serializer.Serialize("header", "123"))
            .Should().BeOfType<string>().Which.Should().Be("123");
    }

    [Theory]
    [InlineData("smb:bool:not-a-bool")]
    [InlineData("smb:byte:not-a-byte")]
    [InlineData("smb:short:not-a-short")]
    [InlineData("smb:int:not-an-int")]
    [InlineData("smb:long:not-a-long")]
    [InlineData("smb:float:not-a-float")]
    [InlineData("smb:double:not-a-double")]
    [InlineData("smb:decimal:not-a-decimal")]
    [InlineData("smb:guid:not-a-guid")]
    [InlineData("smb:datetime:not-a-datetime")]
    [InlineData("smb:datetimeoffset:not-a-datetimeoffset")]
    [InlineData("smb:unknown:value")]
    [InlineData("smb:missing-separator")]
    public void When_MarkedValueIsInvalid_Then_OriginalValueIsReturned(string value)
    {
        var serializer = new DefaultGooglePubSubHeaderSerializer();

        serializer.Deserialize("header", value).Should().Be(value);
    }

    [Fact]
    public void When_NullValueIsDeserialized_Then_NullIsReturned()
    {
        var serializer = new DefaultGooglePubSubHeaderSerializer();

        serializer.Deserialize("header", null).Should().BeNull();
    }
}
