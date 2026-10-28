namespace SlimMessageBus.Host.Mqtt;

/// <summary>
/// Indexes exact topics while preserving the order of overlapping subscriptions.
/// Built once per consumer lifecycle and only read while messages are received.
/// </summary>
internal sealed class MqttTopicMatcher<T> where T : class
{
    private readonly Dictionary<string, (int Index, T Consumer)> _exactMatches = new(StringComparer.Ordinal);
    private readonly (int Index, string Filter, T Consumer)[] _wildcardMatches;

    public MqttTopicMatcher(IEnumerable<(string Filter, T Consumer)> consumers)
    {
        var wildcards = new List<(int Index, string Filter, T Consumer)>();
        var index = 0;
        foreach (var (filter, consumer) in consumers)
        {
            // MQTTnet considers empty filters invalid, so they cannot match.
            if (string.IsNullOrEmpty(filter))
            {
                continue;
            }
            if (filter.Contains('+') || filter.Contains('#'))
            {
                wildcards.Add((index, filter, consumer));
            }
            else
            {
                // Retain the first consumer if an exact path is registered twice
                // (for example, a consumer and the request-response subscription).
                _exactMatches.TryAdd(filter, (index, consumer));
            }
            index++;
        }
        _wildcardMatches = wildcards.ToArray();
    }

    public T FindMatch(string topic)
    {
        if (string.IsNullOrEmpty(topic))
        {
            return null;
        }

        var hasExactMatch = _exactMatches.TryGetValue(topic, out var exactMatch);
        var exactIndex = hasExactMatch ? exactMatch.Index : int.MaxValue;
        foreach (var wildcard in _wildcardMatches)
        {
            // Earlier wildcard subscriptions must still beat a later exact match.
            if (wildcard.Index >= exactIndex)
            {
                break;
            }
            if (MqttTopicFilterComparer.Compare(topic, wildcard.Filter) == MqttTopicFilterCompareResult.IsMatch)
            {
                return wildcard.Consumer;
            }
        }
        return exactMatch.Consumer;
    }
}
