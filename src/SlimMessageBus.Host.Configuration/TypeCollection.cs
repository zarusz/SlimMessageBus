namespace SlimMessageBus.Host;

public class TypeCollection<TInterface> : IEnumerable<Type> where TInterface : class
{
    private readonly Type _interfaceType = typeof(TInterface);
    private readonly List<Type> _innerList = [];

    public void Add(Type type)
    {
        if (!_interfaceType.IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type is not assignable to '{_interfaceType}'.", nameof(type));
        }

        if (_innerList.Contains(type))
        {
            throw new ArgumentException("Type already exists in the collection.", nameof(type));
        }

        _innerList.Add(type);
    }

    public void Add<T>() where T : TInterface
    {
        var type = typeof(T);
        if (_innerList.Contains(type))
        {
            throw new ArgumentException("Type already exists in the collection.", nameof(type)); // NOSONAR
        }

        _innerList.Add(type);
    }

    public bool TryAdd<T>() where T : TInterface
    {
        var type = typeof(T);
        if (_innerList.Contains(type))
        {
            return false;
        }

        _innerList.Add(type);
        return true;
    }

    public void Clear() => _innerList.Clear();

    public bool Contains<T>() where T : TInterface
    {
        return _innerList.Contains(typeof(T));
    }

    public void CopyTo(Type[] array, int arrayIndex) => _innerList.CopyTo(array, arrayIndex);

    public bool Remove<T>() where T : TInterface
    {
        return _innerList.Remove(typeof(T));
    }

    public bool Remove(Type type)
    {
        return _innerList.Remove(type);
    }

    public int Count => _innerList.Count;

    public bool IsReadOnly => false;

    public IEnumerator<Type> GetEnumerator()
    {
        return _innerList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _innerList.GetEnumerator();
    }
}