namespace SlimMessageBus.Host.Collections;

public class RuntimeTypeCache : IRuntimeTypeCache
{
    private readonly IReadOnlyCache<(Type From, Type To), bool> _isAssignable;
    private readonly IReadOnlyCache<Type, TaskOfTypeCache> _taskOfType;
    private readonly IReadOnlyCache<(Type OpenGenericType, Type GenericParameterType), Type> _closedGenericTypeOfOpenGenericType;

    public IReadOnlyCache<(Type ClassType, string MethodName, Type GenericArgument), Func<object, Task<object>>> GenericMethod { get; }

    public IGenericTypeCache<Func<object, object, Func<Task<object>>, IProducerContext, Task<object>>> ProducerInterceptorType { get; }
    public IGenericTypeCache<Func<object, object, Func<Task>, IProducerContext, Task>> PublishInterceptorType { get; }
    public IGenericTypeCache2<Func<object, object, object, IProducerContext, Task>> SendInterceptorType { get; }

    public IGenericTypeCache<Func<object, object, Func<Task<object>>, IConsumerContext, Task<object>>> ConsumerInterceptorType { get; }
    public IGenericTypeCache2<Func<object, object, object, IConsumerContext, Task>> HandlerInterceptorType { get; }

    public IGenericTypeCache<Func<object, object, Func<Task<object>>, IConsumerContext, Exception, Task<ConsumerErrorHandlerResult>>> ConsumerErrorHandlerType { get; }

    public RuntimeTypeCache()
    {
        _isAssignable = new SafeDictionaryWrapper<(Type From, Type To), bool>(x => x.To.IsAssignableFrom(x.From));
        _taskOfType = new SafeDictionaryWrapper<Type, TaskOfTypeCache>(type => new TaskOfTypeCache(type));
        _closedGenericTypeOfOpenGenericType = new SafeDictionaryWrapper<(Type OpenGenericType, Type GenericPatameterType), Type>(x => x.OpenGenericType.MakeGenericType(x.GenericPatameterType));

        GenericMethod = new SafeDictionaryWrapper<(Type ClassType, string MethodName, Type GenericArgument), Func<object, Task<object>>>(key =>
        {
            var genericMethod = key.ClassType
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(x => x.ContainsGenericParameters && x.IsGenericMethodDefinition && x.GetGenericArguments().Length == 1 && x.Name == key.MethodName);

            return ReflectionUtils.GenerateGenericMethodCallToFunc<Func<object, Task<object>>>(genericMethod, [key.GenericArgument]);
        });

        ProducerInterceptorType = new GenericTypeCache<Func<object, object, Func<Task<object>>, IProducerContext, Task<object>>>(
            typeof(IProducerInterceptor<>),
            nameof(IProducerInterceptor<object>.OnHandle));

        PublishInterceptorType = new GenericTypeCache<Func<object, object, Func<Task>, IProducerContext, Task>>(
            typeof(IPublishInterceptor<>),
            nameof(IPublishInterceptor<object>.OnHandle));

        SendInterceptorType = new GenericTypeCache2<Func<object, object, object, IProducerContext, Task>>(
            typeof(ISendInterceptor<,>),
            nameof(ISendInterceptor<object, object>.OnHandle));

        ConsumerInterceptorType = new GenericTypeCache<Func<object, object, Func<Task<object>>, IConsumerContext, Task<object>>>(
            typeof(IConsumerInterceptor<>),
            nameof(IConsumerInterceptor<object>.OnHandle));

        HandlerInterceptorType = new GenericTypeCache2<Func<object, object, object, IConsumerContext, Task>>(
            typeof(IRequestHandlerInterceptor<,>),
            nameof(IRequestHandlerInterceptor<object, object>.OnHandle));

        ConsumerErrorHandlerType = new GenericTypeCache<Func<object, object, Func<Task<object>>, IConsumerContext, Exception, Task<ConsumerErrorHandlerResult>>>(
            typeof(IConsumerErrorHandler<>),
            nameof(IConsumerErrorHandler<object>.OnHandleError));
    }

    public bool IsAssignableFrom(Type from, Type to)
        => _isAssignable[(from, to)];

    public TaskOfTypeCache GetTaskOfType(Type type)
        => _taskOfType[type];

    public Type GetClosedGenericType(Type openGenericType, Type genericParameterType)
        => _closedGenericTypeOfOpenGenericType[(openGenericType, genericParameterType)];
}
