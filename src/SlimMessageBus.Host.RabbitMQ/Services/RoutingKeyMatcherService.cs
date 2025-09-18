namespace SlimMessageBus.Host.RabbitMQ;

/// <summary>
/// Service responsible for matching routing keys against patterns, including wildcard support.
/// Handles RabbitMQ topic exchange routing semantics where * matches exactly one segment 
/// and # matches zero or more segments.
/// </summary>
internal class RoutingKeyMatcherService<T>
{
    /// <summary>
    /// Wildcard character that matches exactly one segment in RabbitMQ topic routing.
    /// </summary>
    private const char SingleSegmentWildcard = '*';

    /// <summary>
    /// Wildcard character that matches zero or more segments in RabbitMQ topic routing.
    /// </summary>
    private const char MultipleSegmentWildcard = '#';

    /// <summary>
    /// Character that separates segments in RabbitMQ routing keys.
    /// </summary>
    private const char SegmentSeparator = '.';

    /// <summary>
    /// String representation of the multiple segment wildcard for pattern comparison.
    /// </summary>
    private const string MultipleSegmentWildcardString = "#";

    /// <summary>
    /// String representation of the single segment wildcard for pattern comparison.
    /// </summary>
    private const string SingleSegmentWildcardString = "*";

    private readonly IDictionary<string, T> _exactMatches;
    private readonly IList<(string Pattern, T Item)> _wildcardPatterns;

    public RoutingKeyMatcherService(IDictionary<string, T> routingKeyItems)
    {
        if (routingKeyItems == null) throw new ArgumentNullException(nameof(routingKeyItems));

        // Separate exact matches from wildcard patterns for performance optimization
        _exactMatches = new Dictionary<string, T>();
        _wildcardPatterns = [];

        foreach (var kvp in routingKeyItems)
        {
            if (ContainsWildcards(kvp.Key))
            {
                _wildcardPatterns.Add((kvp.Key, kvp.Value));
            }
            else
            {
                _exactMatches[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Finds the first item that matches the given routing key.
    /// First tries exact matches for better performance, then wildcard patterns.
    /// </summary>
    /// <param name="routingKey">The routing key to match against</param>
    /// <returns>The matching item, or default(T) if no match found</returns>
    public T FindMatch(string routingKey)
    {
        // First try exact match for better performance
        if (_exactMatches.TryGetValue(routingKey, out var exactMatch))
        {
            return exactMatch;
        }

        // If no exact match and we have wildcard patterns, try wildcard matching
        if (_wildcardPatterns.Count > 0)
        {
            foreach (var (pattern, item) in _wildcardPatterns)
            {
                if (MatchesWildcardPattern(routingKey, pattern))
                {
                    return item;
                }
            }
        }

        return default;
    }

    /// <summary>
    /// Gets all items that have exact routing key matches (no wildcards).
    /// </summary>
    public IEnumerable<T> ExactMatchItems => _exactMatches.Values;

    /// <summary>
    /// Gets all items that have wildcard patterns.
    /// </summary>
    public IEnumerable<(string Pattern, T Item)> WildcardPatternItems => _wildcardPatterns;

    /// <summary>
    /// Gets all items (both exact matches and wildcard patterns).
    /// </summary>
    public IEnumerable<T> AllItems => _exactMatches.Values.Concat(_wildcardPatterns.Select(x => x.Item));

    /// <summary>
    /// Checks if a routing key contains wildcard characters (* or #).
    /// </summary>
    internal static bool ContainsWildcards(string routingKey)
        => !string.IsNullOrEmpty(routingKey) && (routingKey.Contains(SingleSegmentWildcard) || routingKey.Contains(MultipleSegmentWildcard));

    /// <summary>
    /// Matches a message routing key against a wildcard pattern according to RabbitMQ topic exchange semantics.
    /// * matches exactly one segment
    /// # matches zero or more segments
    /// Segments are separated by '.'
    /// </summary>
    /// <param name="messageRoutingKey">The actual routing key from the message</param>
    /// <param name="pattern">The binding pattern that may contain wildcards</param>
    /// <returns>True if the routing key matches the pattern</returns>
    internal static bool MatchesWildcardPattern(string messageRoutingKey, string pattern)
    {
        if (string.IsNullOrEmpty(messageRoutingKey) && string.IsNullOrEmpty(pattern))
        {
            return true;
        }

        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        // Handle the special case where pattern is just "#" - matches anything
        if (pattern == MultipleSegmentWildcardString)
        {
            return true;
        }

        // Handle empty string case - empty string has no segments
        var messageSegments = string.IsNullOrEmpty(messageRoutingKey)
            ? []
            : messageRoutingKey.Split(SegmentSeparator);

        var patternSegments = pattern.Split(SegmentSeparator);

        return MatchSegments(messageSegments, patternSegments, 0, 0);
    }

    private static bool MatchSegments(string[] messageSegments, string[] patternSegments, int messageIndex, int patternIndex)
    {
        // If we've consumed all pattern segments
        if (patternIndex >= patternSegments.Length)
        {
            // Match only if we've also consumed all message segments
            return messageIndex >= messageSegments.Length;
        }

        var currentPattern = patternSegments[patternIndex];

        // Handle # wildcard (zero or more segments)
        if (currentPattern == MultipleSegmentWildcardString)
        {
            // If # is the last pattern segment, it matches all remaining message segments
            if (patternIndex == patternSegments.Length - 1)
            {
                return true;
            }

            // Try matching # with 0, 1, 2, ... segments
            for (var i = messageIndex; i <= messageSegments.Length; i++)
            {
                if (MatchSegments(messageSegments, patternSegments, i, patternIndex + 1))
                {
                    return true;
                }
            }
            return false;
        }

        // If we've consumed all message segments but still have non-# patterns
        if (messageIndex >= messageSegments.Length)
        {
            return false;
        }

        var currentMessage = messageSegments[messageIndex];

        // Handle * wildcard (exactly one segment)
        if (currentPattern == SingleSegmentWildcardString)
        {
            return MatchSegments(messageSegments, patternSegments, messageIndex + 1, patternIndex + 1);
        }

        // Handle exact match
        if (currentPattern == currentMessage)
        {
            return MatchSegments(messageSegments, patternSegments, messageIndex + 1, patternIndex + 1);
        }

        // No match
        return false;
    }
}