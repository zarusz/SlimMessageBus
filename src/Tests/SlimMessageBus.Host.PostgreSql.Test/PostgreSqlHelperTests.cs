namespace SlimMessageBus.Host.PostgreSql.Test;

public class PostgreSqlHelperTests
{
    [Theory]
    [InlineData("messages", "\"messages\"")]
    [InlineData("_messages_1", "\"_messages_1\"")]
    [InlineData("Messages", "\"Messages\"")]
    public void QuoteIdentifier_GivenSafeIdentifier_ThenQuotes(string identifier, string expected)
    {
        var actual = PostgreSqlHelper.QuoteIdentifier(identifier, nameof(identifier));

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1messages")]
    [InlineData("messages;drop")]
    [InlineData("messages-name")]
    [InlineData("messages.name")]
    public void QuoteIdentifier_GivenUnsafeIdentifier_ThenThrows(string identifier)
    {
        var action = () => PostgreSqlHelper.QuoteIdentifier(identifier, nameof(identifier));

        action.Should().Throw<ArgumentException>().WithParameterName("identifier");
    }
}
