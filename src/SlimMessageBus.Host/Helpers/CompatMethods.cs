#if NETSTANDARD2_0

namespace SlimMessageBus.Host;

/// <summary>
/// Helper for netstandard2.0
/// </summary>
public static class DictionaryExtensions
{
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> keyValuePair, out TKey key, out TValue value)
    {
        key = keyValuePair.Key;
        value = keyValuePair.Value;
    }

    public static bool TryAdd<K, V>(this IDictionary<K, V> dict, K key, V value)
    {
        if (!dict.ContainsKey(key))
        {
            dict.Add(key, value);
            return true;
        }
        return false;
    }

    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> items) => new(items);

    public static IEnumerable<IReadOnlyCollection<T>> Chunk<T>(this IEnumerable<T> items, int size)
    {
        var chunk = new List<T>(size);

        foreach (var item in items)
        {
            if (chunk.Count < size)
            {
                chunk.Add(item);
            }
            else
            {
                yield return chunk;
                chunk = new List<T>(size);
            }
        }

        if (chunk.Count > 0)
        {
            yield return chunk;
        }
    }

}

public static class TimeSpanExtensions
{
    public static TimeSpan Multiply(this TimeSpan timeSpan, double factor)
        => TimeSpan.FromMilliseconds(timeSpan.TotalMilliseconds * factor);
}

#endif