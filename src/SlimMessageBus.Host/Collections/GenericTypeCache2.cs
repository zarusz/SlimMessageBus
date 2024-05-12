namespace SlimMessageBus.Host.Collections;

public interface IGenericTypeCache2<TFunc> : IReadOnlyCache<(Type RequestType, Type ResponseType), TFunc>
    where TFunc : Delegate
{
    IEnumerable<object> ResolveAll(IServiceProvider scope, (Type RequestType, Type ResponseType) p);

    public class GenericInterfaceType
    {
        public Type RequestType { get; }
        public Type ResponseType { get; }
        public Type GenericType { get; }
        public Type EnumerableOfGenericType { get; }
        public MethodInfo Method { get; }
        public TFunc Func { get; }

        public GenericInterfaceType(Type requestType, Type responseType, Type genericType, MethodInfo method, TFunc func)
        {
            RequestType = requestType;
            ResponseType = responseType;
            GenericType = genericType;
            EnumerableOfGenericType = typeof(IEnumerable<>).MakeGenericType(genericType);
            Method = method;
            Func = func;
        }
    }
}

/// <summary>
/// Generic Interface Type cache for open generic type interfaces which have two generic parameter e.g. <see cref="ISendInterceptor{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TFunc"></typeparam>
public class GenericTypeCache2<TFunc> : IGenericTypeCache2<TFunc>
    where TFunc : Delegate
{
    private readonly Type _openGenericType;
    private readonly string _methodName;
    private readonly IReadOnlyCache<(Type RequestType, Type ResponseType), IGenericTypeCache2<TFunc>.GenericInterfaceType> _messageTypeToGenericInterfaceType;
    private readonly SafeDictionaryWrapper<(Type RequestType, Type ResponseType), GenericTypeResolveCache> _messageTypeToResolveCache;

    public TFunc this[(Type RequestType, Type ResponseType) key] => _messageTypeToGenericInterfaceType[key].Func;

    public GenericTypeCache2(Type openGenericType, string methodName)
    {
        _openGenericType = openGenericType;
        _methodName = methodName;
        _messageTypeToGenericInterfaceType = new SafeDictionaryWrapper<(Type RequestType, Type ResponseType), IGenericTypeCache2<TFunc>.GenericInterfaceType>(CreateType);
        _messageTypeToResolveCache = new SafeDictionaryWrapper<(Type RequestType, Type ResponseType), GenericTypeResolveCache>();
    }

    private IGenericTypeCache2<TFunc>.GenericInterfaceType CreateType((Type RequestType, Type ResponseType) p)
    {
        var genericType = _openGenericType.MakeGenericType(p.RequestType, p.ResponseType);
        var method = genericType.GetMethod(_methodName) ?? throw new InvalidOperationException($"The method {_methodName} was not found on type {genericType}");
        var func = ReflectionUtils.GenerateMethodCallToFunc<TFunc>(method);
        return new IGenericTypeCache2<TFunc>.GenericInterfaceType(p.RequestType, p.ResponseType, genericType, method, func);
    }

    /// <summary>
    /// Returns the resolved instances, or null if none are registered.
    /// </summary>
    /// <param name="scope"></param>
    /// <param name="messageType"></param>
    /// <returns></returns>
    public IEnumerable<object> ResolveAll(IServiceProvider scope, (Type RequestType, Type ResponseType) p)
    {
        var git = _messageTypeToGenericInterfaceType[p];

        var cacheExists = _messageTypeToResolveCache.TryGet(p, out var lookupCache);
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
                _messageTypeToResolveCache.Set(p, lookupCache);
            }

            if (!lookupCache.IsEmpty)
            {
                return interceptors;
            }
        }
        return null;
    }
}
