namespace SlimMessageBus.Host;

public interface IAssemblyQualifiedNameMessageTypeResolverRedirect
{
    /// <summary>
    /// Returns the Type if it can be resolved for the name, otherwise null.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    Type TryGetType(string name);

    /// <summary>
    /// Returns the name if it can be resolved for the type, otherwise null.
    /// </summary>
    /// <param name="messageType"></param>
    /// <returns></returns>
    string TryGetName(Type messageType);
}