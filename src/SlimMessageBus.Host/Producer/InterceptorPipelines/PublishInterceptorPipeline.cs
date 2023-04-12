namespace SlimMessageBus.Host;

internal class PublishInterceptorPipeline : ProducerInterceptorPipeline<PublishContext>
{
    private readonly IEnumerable<object> _publishInterceptors;
    private readonly Func<object, object, Func<Task>, IProducerContext, Task> _publishInterceptorFunc;
    private IEnumerator<object> _publishInterceptorsEnumerator;
    private bool _publishInterceptorsVisited = false;

    public PublishInterceptorPipeline(MessageBusBase bus, object message, ProducerSettings producerSettings, IServiceProvider currentServiceProvider, PublishContext context, IEnumerable<object> producerInterceptors, IEnumerable<object> publishInterceptors)
        : base(bus, message, producerSettings, currentServiceProvider, context, producerInterceptors)
    {
        _publishInterceptors = publishInterceptors;
        _publishInterceptorFunc = bus.RuntimeTypeCache.PublishInterceptorType[message.GetType()];
        _publishInterceptorsVisited = publishInterceptors is null;
        _publishInterceptorsEnumerator = _publishInterceptors?.GetEnumerator();
    }

    public async Task<object> Next()
    {
        if (!_producerInterceptorsVisited)
        {
            if (_producerInterceptorsEnumerator.MoveNext())
            {
                return await _producerInterceptorFunc(_producerInterceptorsEnumerator.Current, _message, Next, _context);
            }
            _producerInterceptorsVisited = true;
            _producerInterceptorsEnumerator = null;
        }

        if (!_publishInterceptorsVisited)
        {
            if (_publishInterceptorsEnumerator.MoveNext())
            {
                await _publishInterceptorFunc(_publishInterceptorsEnumerator.Current, _message, Next, _context);
                return null;
            }
            _publishInterceptorsVisited = true;
            _publishInterceptorsEnumerator = null;
        }

        if (!_targetVisited)
        {
            _targetVisited = true;
            await _bus.PublishInternal(_message, _context.Path, _context.Headers, _context.CancellationToken, _producerSettings, _currentServiceProvider);
            return null;
        }

        // throw exception as it should never happen
        throw new PublishMessageBusException("The next() was invoked more than once on one of the provided interceptors");
    }
}
