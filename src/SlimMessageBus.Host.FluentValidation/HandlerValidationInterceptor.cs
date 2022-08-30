namespace SlimMessageBus.Host.FluentValidation;

using global::FluentValidation;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public class HandlerValidationInterceptor<T, R> : AbstractValidationInterceptor<T>, IRequestHandlerInterceptor<T, R>
{
    public HandlerValidationInterceptor(IEnumerable<IValidator<T>> validators, IValidationErrorsHandler errorsHandler = null)
        : base(validators, errorsHandler)
    {
    }

    public async Task<R> OnHandle(T message, CancellationToken cancellationToken, Func<Task<R>> next, IMessageBus bus, string path, IReadOnlyDictionary<string, object> headers, object handler)
    {
        await OnValidate(message, cancellationToken).ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }
}