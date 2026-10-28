namespace SlimMessageBus.Host.Mqtt.Test;

using MQTTnet;

public class MqttTopicMatcherTests
{
    [Theory]
    [InlineData("sensors/kitchen/temperature", "sensors/kitchen/temperature", "sensors/#", "consumer-0")]
    [InlineData("sensors/kitchen/temperature", "sensors/#", "sensors/kitchen/temperature", "consumer-0")]
    [InlineData("sensors/kitchen/temperature", "sensors/+/temperature", "sensors/#", "consumer-0")]
    [InlineData("sensors/kitchen/temperature", "other/#", "sensors/kitchen/temperature", "consumer-1")]
    [InlineData("sensors/kitchen/temperature", "other/exact", "sensors/+/temperature", "consumer-1")]
    [InlineData("sensors/kitchen/temperature", "other/exact", "other/#", null)]
    [InlineData("sensors/kitchen/temperature", "sensors/kitchen/temperature", "sensors/kitchen/temperature", "consumer-0")]
    [InlineData("Sensors/kitchen/temperature", "sensors/kitchen/temperature", "other/#", null)]
    [InlineData("$SYS/status", "#", "$SYS/#", "consumer-1")]
    [InlineData("sensors", "sensors/#", "other/exact", "consumer-0")]
    [InlineData("sensors/kitchen/temperature", "sensors/#/invalid", "sensors/kitchen/temperature", "consumer-1")]
    [InlineData(null, "sensors/#", "other/exact", null)]
    [InlineData("", "sensors/#", "other/exact", null)]
    [InlineData("sensors/kitchen/temperature", null, "sensors/kitchen/temperature", "consumer-1")]
    [InlineData("sensors/kitchen/temperature", "", "sensors/kitchen/temperature", "consumer-1")]
    public void FindsFirstMatchingConsumer(string topic, string firstFilter, string secondFilter, string expected)
    {
        var matcher = new MqttTopicMatcher<string>([(firstFilter, "consumer-0"), (secondFilter, "consumer-1")]);

        matcher.FindMatch(topic).Should().Be(expected);
    }

    [Fact]
    public void EmptyMatcherDoesNotMatch()
    {
        new MqttTopicMatcher<string>([]).FindMatch("sensors/kitchen/temperature").Should().BeNull();
    }

    [Fact]
    public void MatchesExistingMqttNetScanForMixedSubscriptionOrders()
    {
        string[] filters = ["sensors/kitchen/temperature", "sensors/+/temperature", "sensors/#", "#", "$SYS/#", "other/exact", "sensors/#/invalid"];
        string[] topics = ["sensors", "sensors/", "sensors//temperature", "sensors/kitchen/temperature", "sensors/kitchen/temperature/extra", "$SYS/status", "other/exact", "other/unknown", "Sensors/kitchen/temperature"];

        // Different insertion orders must preserve the previous first-match result,
        // especially when exact filters and wildcards overlap.
        for (var start = 0; start < filters.Length; start++)
        {
            var ordered = filters.Skip(start).Concat(filters.Take(start))
                .Select((filter, index) => (Filter: filter, Consumer: $"consumer-{index}"))
                .ToArray();
            var matcher = new MqttTopicMatcher<string>(ordered);
            foreach (var topic in topics)
            {
                var expected = ordered.FirstOrDefault(x => MqttTopicFilterComparer.Compare(topic, x.Filter) == MqttTopicFilterCompareResult.IsMatch).Consumer;
                matcher.FindMatch(topic).Should().Be(expected, $"topic {topic} with first filter {ordered[0].Filter}");
            }
        }
    }

    [Fact]
    public async Task ConcurrentReadsPreserveMatchingResults()
    {
        var matcher = new MqttTopicMatcher<string>([("sensors/+/temperature", "wildcard"), ("sensors/kitchen/temperature", "exact"), ("other/exact", "other")]);

        await Parallel.ForEachAsync(Enumerable.Range(0, 1000), (index, _) =>
        {
            matcher.FindMatch(index % 2 == 0 ? "sensors/kitchen/temperature" : "other/exact")
                .Should().Be(index % 2 == 0 ? "wildcard" : "other");
            return ValueTask.CompletedTask;
        });
    }
}
