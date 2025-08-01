﻿namespace SlimMessageBus.Host;

internal class SendInterceptorPipeline<TResponse> : ProducerInterceptorPipeline<SendContext>
{
    private readonly MessageBusBase _bus;
    private readonly Func<object, object, object, IProducerContext, Task> _sendInterceptorFunc;
    private IEnumerator<object> _sendInterceptorsEnumerator;
    private bool _sendInterceptorsVisited = false;

    public SendInterceptorPipeline(MessageBusBase bus, object message, ProducerSettings producerSettings, IMessageBusTarget targetBus, SendContext context, IEnumerable<object> producerInterceptors, IEnumerable<object> sendInterceptors)
        : base(bus.RuntimeTypeCache, message, producerSettings, targetBus, context, producerInterceptors)
    {
        _bus = bus;
        _sendInterceptorFunc = bus.RuntimeTypeCache.SendInterceptorType[(message.GetType(), typeof(TResponse))];
        _sendInterceptorsVisited = sendInterceptors is null;
        _sendInterceptorsEnumerator = sendInterceptors?.GetEnumerator();
    }

    private async Task<object> NextOfObject() => await Next();

    public async Task<TResponse> Next()
    {
        if (!_producerInterceptorsVisited)
        {
            if (_producerInterceptorsEnumerator.MoveNext())
            {
                var response = await _producerInterceptorFunc(_producerInterceptorsEnumerator.Current, _message, NextOfObject, _context);
                return (TResponse)response;
            }
            _producerInterceptorsVisited = true;
            _producerInterceptorsEnumerator = null;
        }

        if (!_sendInterceptorsVisited)
        {
            if (_sendInterceptorsEnumerator.MoveNext())
            {
                var response = await (Task<TResponse>)_sendInterceptorFunc(_sendInterceptorsEnumerator.Current, _message, (object)Next, _context);
                return response;
            }
            _sendInterceptorsVisited = true;
            _sendInterceptorsEnumerator = null;
        }

        if (!_targetVisited)
        {
            _targetVisited = true;
            var response = await _bus.SendInternal<TResponse>(_message,
                                                              _context.Path,
                                                              _message.GetType(),
                                                              typeof(TResponse),
                                                              _producerSettings,
                                                              _context.Created,
                                                              _context.Expires,
                                                              _context.RequestId,
                                                              _context.Headers,
                                                              _targetBus,
                                                              _context.CancellationToken);
            return response;
        }

        // throw exception as it should never happen
        throw new SendMessageBusException("The next() was invoked more than once on one of the provided interceptors");
    }
}