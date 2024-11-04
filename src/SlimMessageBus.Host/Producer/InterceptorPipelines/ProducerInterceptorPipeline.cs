namespace SlimMessageBus.Host;

abstract internal class ProducerInterceptorPipeline<TContext> where TContext : ProducerContext
{
    protected readonly object _message;
    protected readonly ProducerSettings _producerSettings;
    protected readonly IMessageBusTarget _targetBus;
    protected readonly TContext _context;

    protected readonly IEnumerable<object> _producerInterceptors;
    protected readonly Func<object, object, Func<Task<object>>, IProducerContext, Task<object>> _producerInterceptorFunc;
    protected IEnumerator<object> _producerInterceptorsEnumerator;
    protected bool _producerInterceptorsVisited = false;

    protected bool _targetVisited;

    protected ProducerInterceptorPipeline(RuntimeTypeCache runtimeTypeCache, object message, ProducerSettings producerSettings, IMessageBusTarget targetBus, TContext context, IEnumerable<object> producerInterceptors)
    {
        _message = message;
        _producerSettings = producerSettings;
        _targetBus = targetBus;
        _context = context;

        _producerInterceptors = producerInterceptors;
        _producerInterceptorFunc = runtimeTypeCache.ProducerInterceptorType[message.GetType()];
        _producerInterceptorsVisited = producerInterceptors is null;
        _producerInterceptorsEnumerator = _producerInterceptors?.GetEnumerator();
    }
}
