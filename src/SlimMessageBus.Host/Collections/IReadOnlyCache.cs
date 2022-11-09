namespace SlimMessageBus.Host.Collections;

public interface IReadOnlyCache<TKey, TValue>
{
    TValue this[TKey key] { get; }
}
