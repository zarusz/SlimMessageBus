namespace SlimMessageBus.Host.Collections;

public class RuntimeTypeCache : IRuntimeTypeCache
{
    private readonly IReadOnlyCache<(Type From, Type To), bool> _isAssignable;
    private readonly IReadOnlyCache<Type, TaskOfTypeCache> _taskOfType;
    private readonly IReadOnlyCache<(Type OpenGenericType, Type GenericParameterType), Type> _closedGenericTypeOfOpenGenericType;

    public IReadOnlyCache<(Type ClassType, string MethodName, Type GenericArgument), Func<object, object>> GenericMethod { get; }

    public IGenericTypeCache<Func<object, object, Func<Task<object>>, IProducerContext, Task<object>>> ProducerInterceptorType { get; }
    public IGenericTypeCache<Func<object, object, Func<Task>, IProducerContext, Task>> PublishInterceptorType { get; }
    public IGenericTypeCache2<Func<object, object, object, IProducerContext, Task>> SendInterceptorType { get; }

    public IGenericTypeCache<Func<object, object, Func<Task<object>>, IConsumerContext, Task<object>>> ConsumerInterceptorType { get; }
    public IGenericTypeCache2<Func<object, object, object, IConsumerContext, Task>> HandlerInterceptorType { get; }

    public RuntimeTypeCache()
    {
        static Type ReturnTypeFunc(Type responseType) => typeof(Task<>).MakeGenericType(responseType);
        static Type FuncTypeFunc(Type responseType) => typeof(Func<>).MakeGenericType(ReturnTypeFunc(responseType));

        _isAssignable = new SafeDictionaryWrapper<(Type From, Type To), bool>(x => x.To.IsAssignableFrom(x.From));
        _taskOfType = new SafeDictionaryWrapper<Type, TaskOfTypeCache>(type => new TaskOfTypeCache(type));
        _closedGenericTypeOfOpenGenericType = new SafeDictionaryWrapper<(Type OpenGenericType, Type GenericPatameterType), Type>(x => x.OpenGenericType.MakeGenericType(x.GenericPatameterType));

        GenericMethod = new SafeDictionaryWrapper<(Type ClassType, string MethodName, Type GenericArgument), Func<object, object>>(key =>
        {
            var genericMethod = key.ClassType
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(x => x.ContainsGenericParameters && x.IsGenericMethodDefinition && x.GetGenericArguments().Length == 1 && x.Name == key.MethodName);

            return ReflectionUtils.GenerateGenericMethodCallToFunc<Func<object, object>>(genericMethod, new[] { key.GenericArgument }, key.ClassType, typeof(Task<object>));
        });

        ProducerInterceptorType = new GenericTypeCache<Func<object, object, Func<Task<object>>, IProducerContext, Task<object>>>(
            typeof(IProducerInterceptor<>),
            nameof(IProducerInterceptor<object>.OnHandle),
            messageType => typeof(Task<object>),
            messageType => new[] { typeof(Func<Task<object>>), typeof(IProducerContext) });

        PublishInterceptorType = new GenericTypeCache<Func<object, object, Func<Task>, IProducerContext, Task>>(
            typeof(IPublishInterceptor<>),
            nameof(IPublishInterceptor<object>.OnHandle),
            messageType => typeof(Task),
            messageType => new[] { typeof(Func<Task>), typeof(IProducerContext) });

        SendInterceptorType = new GenericTypeCache2<Func<object, object, object, IProducerContext, Task>>(
            typeof(ISendInterceptor<,>),
            nameof(ISendInterceptor<object, object>.OnHandle),
            ReturnTypeFunc,
            responseType => new[] { FuncTypeFunc(responseType), typeof(IProducerContext) });

        ConsumerInterceptorType = new GenericTypeCache<Func<object, object, Func<Task<object>>, IConsumerContext, Task<object>>>(
            typeof(IConsumerInterceptor<>),
            nameof(IConsumerInterceptor<object>.OnHandle),
            messageType => typeof(Task<object>),
            messageType => new[] { typeof(Func<Task<object>>), typeof(IConsumerContext) });

        HandlerInterceptorType = new GenericTypeCache2<Func<object, object, object, IConsumerContext, Task>>(
            typeof(IRequestHandlerInterceptor<,>),
            nameof(IRequestHandlerInterceptor<object, object>.OnHandle),
            ReturnTypeFunc,
            responseType => new[] { FuncTypeFunc(responseType), typeof(IConsumerContext) });
    }

    public bool IsAssignableFrom(Type from, Type to)
        => _isAssignable[(from, to)];

    public TaskOfTypeCache GetTaskOfType(Type type)
        => _taskOfType[type];

    public Type GetClosedGenericType(Type openGenericType, Type genericParameterType)
        => _closedGenericTypeOfOpenGenericType[(openGenericType, genericParameterType)];
}
