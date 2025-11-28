namespace SlimMessageBus.Host;

internal class PublishInterceptorPipeline : ProducerInterceptorPipeline<PublishContext>
{
    private readonly ITransportProducer _bus;
    private readonly Func<object, object, Func<Task>, IProducerContext, Task> _publishInterceptorFunc;
    private IEnumerator<object> _publishInterceptorsEnumerator;
    private bool _publishInterceptorsVisited = false;

    public PublishInterceptorPipeline(ITransportProducer bus, RuntimeTypeCache runtimeTypeCache, object message, ProducerSettings producerSettings, IMessageBusTarget targetBus, PublishContext context, IEnumerable<object> producerInterceptors, IEnumerable<object> publishInterceptors)
        : base(runtimeTypeCache, message, producerSettings, targetBus, context, producerInterceptors)
    {
        _bus = bus;
        _publishInterceptorFunc = runtimeTypeCache.PublishInterceptorType[message.GetType()];
        _publishInterceptorsVisited = publishInterceptors is null;
        _publishInterceptorsEnumerator = publishInterceptors?.GetEnumerator();
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
            // Note: We want to send the producer message type here to preserve polymorphic behavior of the serializers
            await _bus.ProduceToTransport(_message,
                                          _producerSettings.MessageType,
                                          _context.Path,
                                          _context.Headers,
                                          _targetBus,
                                          _context.CancellationToken);
            return null;
        }

        // throw exception as it should never happen
        throw new PublishMessageBusException("The next() was invoked more than once on one of the provided interceptors");
    }
}

