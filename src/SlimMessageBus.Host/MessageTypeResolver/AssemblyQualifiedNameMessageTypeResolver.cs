namespace SlimMessageBus.Host;

using System.Text.RegularExpressions;

using SlimMessageBus.Host.Collections;

/// <summary>
/// <see cref="IMessageTypeResolver"/> that uses the <see cref="Type.AssemblyQualifiedName"/> for mapping the <see cref="Type"/ to a string header value.
/// </summary>
public class AssemblyQualifiedNameMessageTypeResolver : IMessageTypeResolver
{
    private static readonly Regex RedundantAssemblyTokens = new(@"\, (Version|Culture|PublicKeyToken)\=([\w\d.]+)", RegexOptions.None, TimeSpan.FromSeconds(2));

    /// <summary>
    /// Determines whether to emit the Version, Culture and PublicKeyToken along with the Assembly name (for strong assembly naming).
    /// </summary>
    public bool EmitAssemblyStrongName { get; set; } = false;

    private readonly SafeDictionaryWrapper<Type, string> _toNameCache;
    private readonly SafeDictionaryWrapper<string, Type> _toTypeCache;
    private readonly IAssemblyQualifiedNameMessageTypeResolverRedirect[] _items;

    public AssemblyQualifiedNameMessageTypeResolver(IEnumerable<IAssemblyQualifiedNameMessageTypeResolverRedirect> items = null)
    {
        _toNameCache = new SafeDictionaryWrapper<Type, string>(ToNameInternal);
        _toTypeCache = new SafeDictionaryWrapper<string, Type>(ToTypeInternal);
        _items = items is not null ? [.. items] : [];
    }

    private string ToNameInternal(Type messageType)
    {
        if (messageType is null) throw new ArgumentNullException(nameof(messageType));

        string assemblyQualifiedName = null;

        foreach (var item in _items)
        {
            assemblyQualifiedName = item.TryGetName(messageType);
            if (assemblyQualifiedName is not null)
            {
                break;
            }
        }

        assemblyQualifiedName ??= messageType.AssemblyQualifiedName;

        if (!EmitAssemblyStrongName)
        {
            assemblyQualifiedName = RedundantAssemblyTokens.Replace(assemblyQualifiedName, string.Empty);
        }

        return assemblyQualifiedName;
    }

    private Type ToTypeInternal(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));

        foreach (var item in _items)
        {
            var type = item.TryGetType(name);
            if (type is not null)
            {
                return type;
            }
        }

        return Type.GetType(name);
    }

    public string ToName(Type messageType) => _toNameCache.GetOrAdd(messageType);

    public Type ToType(string name) => _toTypeCache.GetOrAdd(name);
}
