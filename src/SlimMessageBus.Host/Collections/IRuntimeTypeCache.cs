﻿namespace SlimMessageBus.Host.Collections;

public interface IRuntimeTypeCache
{
    bool IsAssignableFrom(Type from, Type to);
    TaskOfTypeCache GetTaskOfType(Type type);
    /// <summary>
    /// Cache for generic methods that match this signature <see cref="Func{TResult}"/>.
    /// </summary>
    IReadOnlyCache<(Type ClassType, string MethodName, Type GenericArgument), Func<object, Task<object>>> GenericMethod { get; }

    /// <summary>
    /// Provides a closed generic type for <see cref="openGenericType"> with <see cref="genericParameterType"> as the generic parameter.
    /// </summary>
    /// <param name="openGenericType"></param>
    /// <param name="genericParameterType"></param>
    /// <returns></returns>
    Type GetClosedGenericType(Type openGenericType, Type genericParameterType);

    CollectionTypeInfo GetCollectionTypeInfo(Type type);
}
