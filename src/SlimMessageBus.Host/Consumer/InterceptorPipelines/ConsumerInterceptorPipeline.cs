namespace SlimMessageBus.Host;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;

internal class ConsumerInterceptorPipeline
{
    private readonly RuntimeTypeCache _runtimeTypeCache;
    private readonly IMessageHandler _messageHandler;

    private readonly object _message;
    private readonly IConsumerContext _context;
    private readonly IMessageTypeConsumerInvokerSettings _consumerInvoker;
    private readonly Type _responseType;

    private readonly IEnumerable<object> _consumerInterceptors;
    private readonly Func<object, object, Func<Task<object>>, IConsumerContext, Task<object>> _consumerInterceptorFunc;
    private IEnumerator<object> _consumerInterceptorsEnumerator;
    private bool _consumerInterceptorsVisited;

    private readonly IEnumerable<object> _handlerInterceptors;
    private readonly Func<object, object, object, IConsumerContext, Task> _handlerInterceptorFunc;
    private IEnumerator<object> _handlerInterceptorsEnumerator;
    private bool _handlerInterceptorsVisited;

    private bool _targetVisited;

    public ConsumerInterceptorPipeline(RuntimeTypeCache runtimeTypeCache, IMessageHandler messageHandler, object message, Type responseType, IConsumerContext context, IMessageTypeConsumerInvokerSettings consumerInvoker, IEnumerable<object> consumerInterceptors, IEnumerable<object> handlerInterceptors)
    {
        _runtimeTypeCache = runtimeTypeCache;
        _messageHandler = messageHandler;

        _message = message;
        _context = context;
        _consumerInvoker = consumerInvoker;
        _responseType = responseType;

        _consumerInterceptors = consumerInterceptors;
        _consumerInterceptorFunc = runtimeTypeCache.ConsumerInterceptorType[message.GetType()];
        _consumerInterceptorsVisited = consumerInterceptors is null;
        _consumerInterceptorsEnumerator = consumerInterceptors?.GetEnumerator();

        _handlerInterceptors = handlerInterceptors;
        _handlerInterceptorFunc = responseType != null ? runtimeTypeCache.HandlerInterceptorType[(message.GetType(), responseType)] : null;
        _handlerInterceptorsVisited = handlerInterceptors is null;
        _handlerInterceptorsEnumerator = handlerInterceptors?.GetEnumerator();
    }

    public Task<object> Next()
    {
        if (_responseType == null)
        {
            return Next<object>();
        }

        // Call the relevant generic version of the method
        var methodFunc = _runtimeTypeCache.GenericMethod[(typeof(ConsumerInterceptorPipeline), nameof(Next), _responseType)];
        return (Task<object>)methodFunc(this);
    }

    private async Task<TResponse> NextOfTyped<TResponse>() => (TResponse)await Next();

    protected async Task<object> Next<TResponse>()
    {
        if (!_consumerInterceptorsVisited)
        {
            if (_consumerInterceptorsEnumerator.MoveNext())
            {
                var response = await _consumerInterceptorFunc(_consumerInterceptorsEnumerator.Current, _message, Next<TResponse>, _context);
                return response;
            }
            _consumerInterceptorsVisited = true;
            _consumerInterceptorsEnumerator = null;
        }

        if (!_handlerInterceptorsVisited)
        {
            if (_handlerInterceptorsEnumerator.MoveNext())
            {
                var response = await (Task<TResponse>)_handlerInterceptorFunc(_handlerInterceptorsEnumerator.Current, _message, (object)NextOfTyped<TResponse>, _context);
                return response;
            }
            _handlerInterceptorsVisited = true;
            _handlerInterceptorsEnumerator = null;
        }

        if (!_targetVisited)
        {
            _targetVisited = true;
            return await _messageHandler.ExecuteConsumer(_message, _context, _consumerInvoker, _responseType);
        }

        // throw exception as it should never happen
        throw new ConsumerMessageBusException("The next() was invoked more than once on one of the provided interceptors");
    }
}