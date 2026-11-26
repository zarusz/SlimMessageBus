namespace SlimMessageBus.Host;

internal class PublishInterceptorPipeline : ProducerInterceptorPipeline<PublishContext>
{
    private readonly ITransportProducer _bus;
    private readonly RuntimeTypeCache _runtimeTypeCache;
    private readonly Func<object, object, Func<Task>, IProducerContext, Task> _publishInterceptorFunc;
    private IEnumerator<object> _publishInterceptorsEnumerator;
    private bool _publishInterceptorsVisited = false;

    public PublishInterceptorPipeline(ITransportProducer bus, RuntimeTypeCache runtimeTypeCache, object message, ProducerSettings producerSettings, IMessageBusTarget targetBus, PublishContext context, IEnumerable<object> producerInterceptors, IEnumerable<object> publishInterceptors)
        : base(runtimeTypeCache, message, producerSettings, targetBus, context, producerInterceptors)
    {
        _bus = bus;
        _runtimeTypeCache = runtimeTypeCache;
        _publishInterceptorFunc = runtimeTypeCache.PublishInterceptorType[message.GetType()];
        _publishInterceptorsVisited = publishInterceptors is null;
        _publishInterceptorsEnumerator = publishInterceptors?.GetEnumerator();
    }

    private sealed record DeclaredInterceptor(object Instance, Type MessageType);

    internal static IEnumerable<object> IncludeDeclaredInterceptors(IEnumerable<object> runtimeInterceptors, IEnumerable<object> declaredInterceptors, Type declaredType)
    {
        if (declaredInterceptors == null)
        {
            return runtimeInterceptors;
        }

        return (runtimeInterceptors ?? [])
            .Concat(declaredInterceptors.Select(instance => new DeclaredInterceptor(instance, declaredType)))
            .OrderBy(interceptor => ((interceptor is DeclaredInterceptor declared ? declared.Instance : interceptor) as IInterceptorWithOrder)?.Order ?? 0);
    }

    public async Task<object> Next()
    {
        if (!_producerInterceptorsVisited)
        {
            if (_producerInterceptorsEnumerator.MoveNext())
            {
                var interceptor = _producerInterceptorsEnumerator.Current;
                return interceptor is DeclaredInterceptor declared
                    ? await _runtimeTypeCache.ProducerInterceptorType[declared.MessageType](declared.Instance, _message, Next, _context)
                    : await _producerInterceptorFunc(interceptor, _message, Next, _context);
            }
            _producerInterceptorsVisited = true;
            _producerInterceptorsEnumerator = null;
        }

        if (!_publishInterceptorsVisited)
        {
            if (_publishInterceptorsEnumerator.MoveNext())
            {
                var interceptor = _publishInterceptorsEnumerator.Current;
                if (interceptor is DeclaredInterceptor declared)
                {
                    await _runtimeTypeCache.PublishInterceptorType[declared.MessageType](declared.Instance, _message, Next, _context);
                }
                else
                {
                    await _publishInterceptorFunc(interceptor, _message, Next, _context);
                }
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

