namespace SlimMessageBus.Host.Collections;

public interface IReadOnlyCache<in TKey, out TValue>
{
    TValue this[TKey key] { get; }
}
