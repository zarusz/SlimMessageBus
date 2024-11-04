namespace SlimMessageBus.Host.Collections;

public class CollectionTypeInfo
{
    public Type ItemType { get; init; }
    public Func<object, IEnumerable<object>> ToCollection { get; init; }
}
