namespace SlimMessageBus.Host.Collections;

using SlimMessageBus.Host.Interceptor;

public interface IGenericTypeCache<TFunc> : IReadOnlyCache<Type, TFunc>
    where TFunc : Delegate
{
    IEnumerable<object> ResolveAll(IServiceProvider scope, Type messageType);

    public class GenericInterfaceType
    {
        public Type MessageType { get; }
        public Type GenericType { get; }
        public Type EnumerableOfGenericType { get; }
        public MethodInfo Method { get; }
        public TFunc Func { get; }

        public GenericInterfaceType(Type messageType, Type genericType, MethodInfo method, TFunc func)
        {
            MessageType = messageType;
            GenericType = genericType;
            EnumerableOfGenericType = typeof(IEnumerable<>).MakeGenericType(genericType);
            Method = method;
            Func = func;
        }
    }
}

/// <summary>
/// Generic Interface Type cache for open generic type interfaces which have one generic parameter e.g. <see cref="IProducerInterceptor{TMessage}"/>.
/// </summary>
/// <typeparam name="TFunc"></typeparam>
public class GenericTypeCache<TFunc> : IGenericTypeCache<TFunc>
    where TFunc : Delegate
{
    private readonly Type _openGenericType;
    private readonly string _methodName;
    private readonly Func<Type, Type> _returnTypeFunc;
    private readonly Func<Type, Type[]> _argumentTypesFunc;
    private readonly IReadOnlyCache<Type, IGenericTypeCache<TFunc>.GenericInterfaceType> _messageTypeToGenericInterfaceType;
    private readonly SafeDictionaryWrapper<Type, GenericTypeResolveCache> _messageTypeToResolveCache;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="openGenericType">The open generic type e.g. <see cref="IProducerInterceptor{TMessage}>"/>.</param>
    /// <param name="methodName">The method name on the open generic type.</param>
    /// <param name="returnTypeFunc">The return type of the method.</param>
    /// <param name="argumentTypes">Additional method arguments (in addition to the message type which is the open generyc type param).</param>
    public GenericTypeCache(Type openGenericType, string methodName, Func<Type, Type> returnTypeFunc, Func<Type, Type[]> argumentTypesFunc = null)
    {
        _openGenericType = openGenericType;
        _methodName = methodName;
        _returnTypeFunc = returnTypeFunc;
        _argumentTypesFunc = argumentTypesFunc;
        _messageTypeToGenericInterfaceType = new SafeDictionaryWrapper<Type, IGenericTypeCache<TFunc>.GenericInterfaceType>(CreateType);
        _messageTypeToResolveCache = new SafeDictionaryWrapper<Type, GenericTypeResolveCache>();
    }

    private IGenericTypeCache<TFunc>.GenericInterfaceType CreateType(Type messageType)
    {
        var genericType = _openGenericType.MakeGenericType(messageType);
        var method = genericType.GetMethod(_methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public) ?? throw new InvalidOperationException($"The method {_methodName} was not found on type {genericType}");
        var methodArguments = new[] { messageType }.Concat(_argumentTypesFunc?.Invoke(messageType) ?? Enumerable.Empty<Type>()).ToArray();
        var returnType = _returnTypeFunc(messageType);
        var func = ReflectionUtils.GenerateMethodCallToFunc<TFunc>(method, genericType, returnType, methodArguments);
        return new IGenericTypeCache<TFunc>.GenericInterfaceType(messageType, genericType, method, func);
    }

    public TFunc this[Type key] => _messageTypeToGenericInterfaceType[key].Func;

    /// <summary>
    /// Returns the resolved instances, or null if none are registered.
    /// </summary>
    /// <param name="scope"></param>
    /// <param name="messageType"></param>
    /// <returns></returns>
    public IEnumerable<object> ResolveAll(IServiceProvider scope, Type messageType)
    {
        var git = _messageTypeToGenericInterfaceType[messageType];

        var cacheExists = _messageTypeToResolveCache.TryGet(messageType, out var lookupCache);
        if (!cacheExists || !lookupCache.IsEmpty)
        {
            var interceptors = (IEnumerable<object>)scope.GetService(git.EnumerableOfGenericType);

            if (!cacheExists)
            {
                lookupCache = new GenericTypeResolveCache
                {
                    IsEmpty = interceptors == null || !interceptors.Any(),
                    IsSortRequred = interceptors != null && interceptors.OfType<IInterceptorWithOrder>().Any()
                };
                _messageTypeToResolveCache.Set(messageType, lookupCache);
            }

            if (!lookupCache.IsEmpty)
            {
                return lookupCache.IsSortRequred
                    ? interceptors.OrderBy(x => (x as IInterceptorWithOrder)?.Order ?? 0)
                    : interceptors;
            }
        }
        return null;
    }
}
