namespace SlimMessageBus.Host.RabbitMQ.Test.Services;

public class RoutingKeyMatcherServiceTests
{
    [Theory]
    [InlineData("exact.match", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("pattern.*", true)]
    [InlineData("pattern.#", true)]
    [InlineData("*.pattern", true)]
    [InlineData("#.pattern", true)]
    [InlineData("pattern.*.more", true)]
    [InlineData("pattern.#.more", true)]
    [InlineData("#", true)]
    [InlineData("*", true)]
    public void When_CheckingForWildcards_Given_RoutingKey_Then_ShouldDetectCorrectly(string routingKey, bool expectedResult)
    {
        // Act
        var result = RoutingKeyMatcherService<object>.ContainsWildcards(routingKey);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    // Single asterisk tests
    [InlineData("regions.na.cities.toronto", "regions.na.cities.*", true, "Single asterisk should match exactly one segment")]
    [InlineData("regions.na.cities.newyork", "regions.na.cities.*", true, "Single asterisk should match exactly one segment")]
    [InlineData("regions.na.cities", "regions.na.cities.*", false, "Single asterisk requires exactly one segment")]
    [InlineData("regions.na.cities.toronto.downtown", "regions.na.cities.*", false, "Single asterisk matches only one segment")]
    [InlineData("", "*", false, "Single asterisk requires one segment")]
    [InlineData("single", "*", true, "Single asterisk matches one segment")]
    [InlineData("too.many", "*", false, "Single asterisk matches only one segment")]
    // Multiple asterisks tests
    [InlineData("region.na.city.toronto", "region.*.city.*", true, "Multiple asterisks should match their respective segments")]
    [InlineData("region.eu.city.london", "region.*.city.*", true, "Multiple asterisks should match their respective segments")]
    [InlineData("region.na.state.california", "region.*.city.*", false, "Multiple asterisks require exact pattern match")]
    [InlineData("region.na.city", "region.*.city.*", false, "Multiple asterisks require all segments")]
    [InlineData("region.na.city.toronto.downtown", "region.*.city.*", false, "Multiple asterisks require exact segment count")]
    public void When_MatchingWildcardPattern_Given_AsteriskPatterns_Then_ShouldMatchCorrectly(
        string messageKey, string pattern, bool expectedMatch, string reason)
    {
        // Act
        var result = RoutingKeyMatcherService<object>.MatchesWildcardPattern(messageKey, pattern);

        // Assert
        result.Should().Be(expectedMatch, $"{reason}: Pattern '{pattern}' and message '{messageKey}'");
    }

    [Theory]
    // Hash wildcard tests
    [InlineData("audit.events.users.signup", "audit.events.#", true, "Hash should match zero or more segments")]
    [InlineData("audit.events.orders.placed", "audit.events.#", true, "Hash should match zero or more segments")]
    [InlineData("audit.events", "audit.events.#", true, "Hash should match zero segments")]
    [InlineData("audit.users", "audit.events.#", false, "Hash requires prefix match")]
    [InlineData("audit.events.users.signup.completed", "audit.events.#", true, "Hash should match multiple segments")]
    // Hash-only tests
    [InlineData("any.routing.key", "#", true, "Hash-only should match anything")]
    [InlineData("", "#", true, "Hash-only should match empty")]
    [InlineData("single", "#", true, "Hash-only should match single segment")]
    [InlineData("very.long.routing.key.with.many.segments", "#", true, "Hash-only should match many segments")]
    // Mixed hash and asterisk tests
    [InlineData("orders.processed.region.na", "orders.#.region.*", true, "Hash + asterisk combination")]
    [InlineData("orders.created.cancelled.region.eu", "orders.#.region.*", true, "Hash + asterisk combination")]
    [InlineData("orders.region.na", "orders.#.region.*", true, "Hash can match zero segments")]
    [InlineData("orders.processed.state.california", "orders.#.region.*", false, "Hash + asterisk requires pattern match")]
    [InlineData("orders.processed.region", "orders.#.region.*", false, "Hash + asterisk requires all segments")]
    [InlineData("orders.processed.region.na.extra", "orders.#.region.*", false, "Hash + asterisk requires exact ending")]
    // Hash followed by asterisk edge cases
    [InlineData("prefix.segment1.suffix", "#.*.suffix", true, "Hash + asterisk + literal")]
    [InlineData("segment1.suffix", "#.*.suffix", true, "Hash matches zero, asterisk matches one")]
    [InlineData("prefix.extra.segment1.suffix", "#.*.suffix", true, "Hash matches multiple segments")]
    [InlineData("suffix", "#.*.suffix", false, "Hash + asterisk requires at least one segment for asterisk")]
    [InlineData("prefix.suffix", "#.*.suffix", true, "Hash matches zero, asterisk matches prefix")]
    // Asterisk followed by hash tests
    [InlineData("segment1.segment2", "*.#", true, "Asterisk + hash combination")]
    [InlineData("segment1", "*.#", true, "Asterisk matches one, hash matches zero")]
    [InlineData("", "*.#", false, "Asterisk requires one segment")]
    [InlineData("segment1.segment2.segment3", "*.#", true, "Asterisk + hash with multiple segments")]
    public void When_MatchingWildcardPattern_Given_HashPatterns_Then_ShouldMatchCorrectly(
        string messageKey, string pattern, bool expectedMatch, string reason)
    {
        // Act
        var result = RoutingKeyMatcherService<object>.MatchesWildcardPattern(messageKey, pattern);

        // Assert
        result.Should().Be(expectedMatch, $"{reason}: Pattern '{pattern}' and message '{messageKey}'");
    }

    [Theory]
    [InlineData("exact.match", "exact.match", true)]
    [InlineData("different.key", "exact.match", false)]
    [InlineData("", "", true)]
    [InlineData("", "nonempty", false)]
    [InlineData("nonempty", "", false)]
    public void When_MatchingWildcardPattern_Given_ExactPatterns_Then_ShouldMatchExactly(string messageKey, string pattern, bool expectedMatch)
    {
        // Act
        var result = RoutingKeyMatcherService<object>.MatchesWildcardPattern(messageKey, pattern);

        // Assert
        result.Should().Be(expectedMatch, $"Pattern '{pattern}' should {(expectedMatch ? "" : "not ")}match '{messageKey}'");
    }

    [Theory]
    [InlineData("a.b.c.d.e", "a.#.e", true, "Hash in middle should match multiple segments")]
    [InlineData("a.e", "a.#.e", true, "Hash in middle should match zero segments")]
    [InlineData("a.x.y.z.e", "a.#.e", true, "Hash in middle should match any segments")]
    [InlineData("a.b.c.d.f", "a.#.e", false, "Hash requires exact prefix and suffix")]
    [InlineData("b.x.y.z.e", "a.#.e", false, "Hash requires exact prefix")]
    public void When_MatchingWildcardPattern_Given_ComplexPatterns_Then_ShouldMatchCorrectly(
        string messageKey, string pattern, bool expectedMatch, string reason)
    {
        // Act
        var result = RoutingKeyMatcherService<object>.MatchesWildcardPattern(messageKey, pattern);

        // Assert
        result.Should().Be(expectedMatch, $"{reason}: Pattern '{pattern}' and message '{messageKey}'");
    }

    [Fact]
    public void When_MatchingWildcardPattern_Given_NullInputs_Then_ShouldHandleCorrectly()
    {
        // Act & Assert - Null message key scenarios
        RoutingKeyMatcherService<object>.MatchesWildcardPattern(null, "#").Should().Be(true, "Hash should match null/empty");
        RoutingKeyMatcherService<object>.MatchesWildcardPattern(null, "*").Should().Be(false, "Asterisk requires one segment");
        RoutingKeyMatcherService<object>.MatchesWildcardPattern(null, "").Should().Be(true, "Empty pattern matches null");
        RoutingKeyMatcherService<object>.MatchesWildcardPattern(null, "exact").Should().Be(false, "Exact pattern doesn't match null");
        
        // Null pattern scenario
        RoutingKeyMatcherService<object>.MatchesWildcardPattern("any.key", null).Should().Be(false, "Null pattern should not match anything");
    }

    [Fact]
    public void When_CreatingService_Given_RoutingKeyItems_Then_ShouldSeparateExactAndWildcardPatterns()
    {
        // Arrange
        var items = new Dictionary<string, string>
        {
            ["exact.match"] = "ExactProcessor",
            ["wildcard.*"] = "WildcardProcessor1",
            ["another.exact"] = "AnotherExactProcessor",
            ["#.hash"] = "WildcardProcessor2"
        };

        // Act
        var service = new RoutingKeyMatcherService<string>(items);

        // Assert
        service.ExactMatchItems.Should().Contain(new[] { "ExactProcessor", "AnotherExactProcessor" });
        service.WildcardPatternItems.Should().HaveCount(2);
        service.WildcardPatternItems.Should().Contain(x => x.Pattern == "wildcard.*" && x.Item == "WildcardProcessor1");
        service.WildcardPatternItems.Should().Contain(x => x.Pattern == "#.hash" && x.Item == "WildcardProcessor2");
        service.AllItems.Should().HaveCount(4);
    }

    [Fact]
    public void When_FindingMatch_Given_ExactRoutingKey_Then_ShouldReturnExactMatch()
    {
        // Arrange
        var items = new Dictionary<string, string>
        {
            ["exact.match"] = "ExactProcessor",
            ["wildcard.*"] = "WildcardProcessor"
        };
        var service = new RoutingKeyMatcherService<string>(items);

        // Act
        var result = service.FindMatch("exact.match");

        // Assert
        result.Should().Be("ExactProcessor");
    }

    [Fact]
    public void When_FindingMatch_Given_WildcardRoutingKey_Then_ShouldReturnWildcardMatch()
    {
        // Arrange
        var items = new Dictionary<string, string>
        {
            ["exact.match"] = "ExactProcessor",
            ["wildcard.*"] = "WildcardProcessor"
        };
        var service = new RoutingKeyMatcherService<string>(items);

        // Act
        var result = service.FindMatch("wildcard.test");

        // Assert
        result.Should().Be("WildcardProcessor");
    }

    [Fact]
    public void When_FindingMatch_Given_NoMatch_Then_ShouldReturnDefault()
    {
        // Arrange
        var items = new Dictionary<string, string>
        {
            ["exact.match"] = "ExactProcessor",
            ["wildcard.*"] = "WildcardProcessor"
        };
        var service = new RoutingKeyMatcherService<string>(items);

        // Act
        var result = service.FindMatch("no.match");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void When_CreatingService_Given_NullItems_Then_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new RoutingKeyMatcherService<string>(null);
        act.Should().Throw<ArgumentNullException>();
    }
}