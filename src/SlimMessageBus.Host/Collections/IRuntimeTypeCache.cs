namespace SlimMessageBus.Host.Collections;

public interface IRuntimeTypeCache
{
    bool IsAssignableFrom(Type from, Type to);
    TaskOfTypeCache GetTaskOfType(Type type);
    /// <summary>
    /// Cache for generic methods that match this signature <see cref="Func{TResult}"/>.
    /// </summary>
    IReadOnlyCache<(Type ClassType, string MethodName, Type GenericArgument), Func<object, object>> GenericMethod { get; }
}
